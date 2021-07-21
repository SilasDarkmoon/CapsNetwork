local pb = require("pb")
pb.option("enum_as_value")
pb.load(require("protocols.LuaTransfer"))

local validKeyTypes = {
    ["string"] = true,
    ["number"] = true,
}
local validValTypes = {
    ["string"] = true,
    ["number"] = true,
    ["boolean"] = true,
    ["table"] = true,
}
local proto_LuaValueTypeEnum = {
    ["o"] = 0, -- Object
    ["a"] = 1, -- Array
    ["r"] = 2, -- Reference
    ["s"] = 3, -- String
    ["n"] = 4, -- Number
    ["b"] = 5, -- Boolean
    ["x"] = 6, -- Null(Deleted)

    ["Object"] = 0,
    ["Array"] = 1,
    ["Reference"] = 2,
    ["String"] = 3,
    ["Number"] = 4,
    ["Boolean"] = 5,
    ["Null"] = 6,
    ["Deleted"] = 6,

    ["string"] = 3,
    ["number"] = 4,
    ["boolean"] = 5,
}
local proto_LuaValueTypeEnumNames = {
    [0] = "o",
    [1] = "a",
    [2] = "r",
    [3] = "s",
    [4] = "n",
    [5] = "b",
    [6] = "x",
}

local lua2pb = {}

-- make a new table (called 'tar') that contains only data in 'tab'.
-- any userdata / function / metatable / local-variables / ... will be omitted.
-- NOTICE: 'tar' may have multiple references to one same sub-table in multiple fields.
function lua2pb.extractDataFromTable(tab)
    local tab2tar = {}
    local emptytar = {}
    local function findOrCreateTable(tab)
        local tar = tab2tar[tab]
        if tar then
            return tar, true
        else
            tar = {}
            tab2tar[tab] = tar
            emptytar[tar] = tab
            return tar, false
        end
    end

    local workList = {}
    local workIndex = 0
    local function extractToTable()
        while workIndex <= #workList do
            local tar, tab = unpack(workList[workIndex])
            workIndex = workIndex + 1

            if emptytar[tar] then
                emptytar[tar] = nil

                local keyCount = 0
                for k, v in pairs(tab) do
                    local kt = type(k)
                    local vt = type(v)
                    if validKeyTypes[kt] and (validValTypes[vt] or table.isudtable(v)) then
                        keyCount = keyCount + 1
                    end
                end
                local isarray = keyCount > 0 and keyCount == #tab
                if isarray then
                    for i, v in ipairs(tab) do
                        local vt = type(v)
                        if vt == "table" or table.isudtable(v) then
                            local child, found = findOrCreateTable(v)
                            tar[i] = child
                            if not found then
                                workList[#workList + 1] = { child, v }
                            end
                        elseif validValTypes[vt] then
                            tar[i] = v
                        end
                    end
                else
                    local keys = {}
                    for k, v in pairs(tab) do
                        if type(k) == "string" then
                            keys[#keys + 1] = k
                        end
                    end
                    table.sort(keys)
                    for i, k in ipairs(keys) do
                        local v = tab[k]
                        local vt = type(v)
                        if vt == "table" or table.isudtable(v) then
                            local child, found = findOrCreateTable(v)
                            tar[k] = child
                            if not found then
                                workList[#workList + 1] = { child, v }
                            end
                        elseif validValTypes[vt] then
                            tar[k] = v
                        end
                    end
                end
            end
        end
    end

    local tar = findOrCreateTable(tab)
    workList[1] = { tar, tab }
    workIndex = 1
    extractToTable()
    return tar
end

-- convert any table in 'tab' to { i = id, t = "a"/"o", v = {} }
-- if we have more than one reference to one same table, the following references will be converted like: { i = id, t = "r" }
-- this will ensure that the encoder (json encoder for example) will not fall into dead recursion.
-- this should be used on result of lua2pb.extractDataFromTable(tab)
-- TODO: how to make id stable. (won't change between different machines)
function lua2pb.convertDataTableToPlain(tab)
    local tab2idinfo = {}
    local nextid = 1
    local function findOrCreateTable(tab)
        local info = tab2idinfo[tab]
        if info then
            local id = info.id
            local tar = info.tar
            if not id then
                id = nextid
                nextid = nextid + 1
                info.id = id
                tar.i = id
            end
            return { i = id, t = "r" }
        else
            local tar = { v = {} }
            tab2idinfo[tab] = { tar = tar }
            return tar
        end
    end

    local workList = {}
    local workIndex = 0
    local function convertToPlain()
        while workIndex <= #workList do
            local tar, tab = unpack(workList[workIndex])
            workIndex = workIndex + 1
            
            if not tar.t then
                local isarray = type(next(tab)) == "number"
                if isarray then
                    tar.t = "a"
                    for i, v in ipairs(tab) do
                        if type(v) == "table" or table.isudtable(v) then
                            local child = findOrCreateTable(v)
                            tar.v[i] = child
                            if not child.t then
                                workList[#workList + 1] = { child, v }
                            end
                        else
                            tar.v[i] = v
                        end
                    end
                else
                    tar.t = "o"
                    local keys = {}
                    for k, v in pairs(tab) do
                        keys[#keys + 1] = k
                    end
                    table.sort(keys)
                    for i, k in ipairs(keys) do
                        local v = tab[k]
                        if type(v) == "table" or table.isudtable(v) then
                            local child = findOrCreateTable(v)
                            tar.v[k] = child
                            if not child.t then
                                workList[#workList + 1] = { child, v }
                            end
                        else
                            tar.v[k] = v
                        end
                    end
                end
            end
        end
    end

    local tar = findOrCreateTable(tab)
    workList[1] = { tar, tab }
    workIndex = 1
    convertToPlain()
    return tar
end

-- the string of field keys will be stored in a global array
-- if a table is not an array, it will be like { i = id, t = "o", k = { 1,3,5 }, v = { v1, v2, v3 } }. The 1,3,5 in 'k' means the field key is the 1st/3rd/5th string in the global key array.
-- this will decrease the data amount to transfer. this will also ensure the sequence to apply to the receiver lua table object.
-- this should be used on result of lua2pb.convertDataTableToPlain(tab)
function lua2pb.convertPlainDataToCompact(tab)
    local keydict = {}
    local keylist = {}
    local nextkeyid = 1
    
    local function extractKey(key)
        local id = keydict[key]
        if not id then
            id = nextkeyid
            nextkeyid = nextkeyid + 1
            keylist[id] = key
            keydict[key] = id
        end
        return id
    end

    local function convertTable(tab)
        local tar = { i = tab.i, t = tab.t }
        if tab.v then
            tar.v = {}
            if tab.t == "a" then
                for i, v in ipairs(tab.v) do
                    if type(v) == "table" or table.isudtable(v) then
                        tar.v[i] = convertTable(v)
                    else
                        tar.v[i] = v
                    end
                end
            elseif tab.t == "o" then
                tar.k = {}
                local keys = {}
                for k, v in pairs(tab.v) do
                    keys[#keys + 1] = k
                end
                table.sort(keys)
                for i, k in ipairs(keys) do
                    local v = tab.v[k]
                    tar.k[i] = extractKey(k)
                    if type(v) == "table" or table.isudtable(v) then
                        tar.v[i] = convertTable(v)
                    else
                        tar.v[i] = v
                    end
                end
            end
        end
        return tar
    end

    local data = convertTable(tab)
    return { k = keylist, v = data }
end

-- convert to the struct to CompactLuaValueProto defined in LuaTransfer.proto
function lua2pb.convertCompactDataToProtobuf(tab)
    local function convertValue(val)
        local valtype = type(val)
        if valtype == "string" then
            if val == "\024" then
                return { Type = proto_LuaValueTypeEnum.Deleted, ValStr = val }
            else
                return { Type = proto_LuaValueTypeEnum[valtype], ValStr = val }
            end
        elseif valtype == "number" then
            return { Type = proto_LuaValueTypeEnum[valtype], ValNum = val }
        elseif valtype == "boolean" then
            return { Type = proto_LuaValueTypeEnum[valtype], ValBool = val }
        elseif valtype == "table" or table.isudtable(val) then
            local converted = {
                Type = proto_LuaValueTypeEnum[val.t],
                ValTable = {
                    Id = val.i,
                    KeyIds = val.k,
                }
            }
            if val.v then
                local convertedVals = {}
                converted.ValTable.Vals = convertedVals
                for i, v in ipairs(val.v) do
                    convertedVals[#convertedVals + 1] = convertValue(v)
                end
            end
            return converted
        end
    end

    if type(tab) == "table" or table.isudtable(tab) then
        return {
            Keys = tab.k,
            Val = convertValue(tab.v),
        }
    else
        return {
            Val = convertValue(tab),
        }
    end
end

function lua2pb.encode(tab)
    if type(tab) == "table" or table.isudtable(tab) then
        local data = lua2pb.extractDataFromTable(tab)
        local plain = lua2pb.convertDataTableToPlain(data)
        local compact = lua2pb.convertPlainDataToCompact(plain)
        local ptab = lua2pb.convertCompactDataToProtobuf(compact)

        local raw = pb.encode("protocols.CompactLuaValueProto", ptab)
        return raw
    else
        local ptab = lua2pb.convertCompactDataToProtobuf(tab)

        local raw = pb.encode("protocols.CompactLuaValueProto", ptab)
        return raw
    end
end

function lua2pb.convertProtobufToCompactData(tab)
    local function convertValue(val)
        local ptype = val.Type
        if ptype == proto_LuaValueTypeEnum.Deleted then
            return "\024"
        elseif ptype == proto_LuaValueTypeEnum.String then
            return val.ValStr
        elseif ptype == proto_LuaValueTypeEnum.Number then
            return val.ValNum
        elseif ptype == proto_LuaValueTypeEnum.Boolean then
            return val.ValBool
        elseif ptype == proto_LuaValueTypeEnum.Object or ptype == proto_LuaValueTypeEnum.Array or ptype == proto_LuaValueTypeEnum.Reference then
            local valtab = val.ValTable
            local converted = {
                t = proto_LuaValueTypeEnumNames[ptype],
                i = valtab.Id,
                k = valtab.KeyIds,
            }
            if ptype ~= proto_LuaValueTypeEnum.Reference then
                local convertedVals = {}
                converted.v = convertedVals
                if valtab.Vals then
                    for i, v in ipairs(valtab.Vals) do
                        convertedVals[#convertedVals + 1] = convertValue(v)
                    end
                end
            end
            return converted
        end
    end

    if tab.Keys and #tab.Keys > 0 then
        -- this is a table
        return {
            k = tab.Keys,
            v = convertValue(tab.Val)
        }, true
    elseif (type(tab.Val) == "table" or table.isudtable(tab.Val)) and tab.Val.Type then
        -- this is a simple value stored in CompactLuaValueProto
        return convertValue(tab.Val), tab.Val.Type == proto_LuaValueTypeEnum.Object or tab.Val.Type == proto_LuaValueTypeEnum.Array or tab.Val.Type == proto_LuaValueTypeEnum.Reference
    else
        -- this is a simple value
        return convertValue(tab), false
    end
end

function lua2pb.convertCompactToPlainData(tab)
    local keylist = tab.k
    local data = tab.v

    local function convertTable(tab)
        local tar = { i = tab.i, t = tab.t }
        if tab.v then
            tar.v = {}
            if tab.t == "a" then
                for i, v in ipairs(tab.v) do
                    if type(v) == "table" or table.isudtable(v) then
                        tar.v[i] = convertTable(v)
                    else
                        tar.v[i] = v
                    end
                end
            elseif tab.t == "o" then
                for i, kid in ipairs(tab.k) do
                    local v = tab.v[i]
                    local key = keylist[kid] or tostring(kid)
                    if type(v) == "table" or table.isudtable(v) then
                        tar.v[key] = convertTable(v)
                    else
                        tar.v[key] = v
                    end
                end
            end
        end
        return tar
    end

    return convertTable(data)
end

function lua2pb.convertPlainToDataTable(tab)
    local id2tar = {}
    local tar2id = {}

    local function convertTable(tab)
        local id = tab.i
        local tar
        local shouldConvertChildren = true
        if not id or id == 0 then
            tar = {}
        else
            tar = id2tar[id]
            if tar then
                shouldConvertChildren = tar2id[tar]
            else
                tar = {}
                id2tar[id] = tar
                tar2id[tar] = id
            end
        end
        if not shouldConvertChildren then
            return tar
        end
        if tab.t == "r" then
            return tar
        end
        tar2id[tar] = nil
        if tab.t == "a" then
            for i, v in ipairs(tab.v) do
                if type(v) == "table" or table.isudtable(v) then
                    tar[i] = convertTable(v)
                else
                    tar[i] = v
                end
            end
        elseif tab.t == "o" then
            for k, v in pairs(tab.v) do
                if type(v) == "table" or table.isudtable(v) then
                    tar[k] = convertTable(v)
                else
                    tar[k] = v
                end
            end
        end
        return tar
    end

    return convertTable(tab)
end

function lua2pb.decode(raw)
    local ptab = pb.decode("protocols.CompactLuaValueProto", raw)
    local compact, istable = lua2pb.convertProtobufToCompactData(ptab)
    if not istable then
        return compact
    else
        local plain = lua2pb.convertCompactToPlainData(compact)
        local data = lua2pb.convertPlainToDataTable(plain)
        return data
    end
end

function lua2pb.cloneDataWithReference(tab)
    local parsed = {}
    local function cloneTable(tab)
        if type(tab) ~= "table" and not table.isudtable(tab) then
            return tab
        else
            local cloned = parsed[tab]
            if not cloned then
                cloned = {}
                parsed[tab] = cloned

                for k, v in pairs(tab) do
                    cloned[k] = cloneTable(v)
                end
            end
            return cloned
        end
    end

    return cloneTable(tab)
end

function lua2pb.extractDiffTable(src, dst)
    -- first we clone the dst and then directly modify the cloned dst and then use the cloned dst as diff.
    local diff = lua2pb.cloneDataWithReference(dst)
    local parsed = {}
    local parsed_src = {}
    local tables_used_as_new_value = {}
    local function extractDiff(src, dst)
        if src == dst then
            -- in this condition, dst and src can not be tables because dst is a cloned copy
            return nil
        else
            if dst == nil then
                return "\024"
            elseif type(dst) == "table" or table.isudtable(dst) then
                if type(src) ~= "table" and not table.isudtable(src) then
                    tables_used_as_new_value[dst] = true
                    return dst
                    -- in this condition, we do not mark parsed[dst]
                    -- if it is not referenced by other field, we'd better keep its full data
                end
                if parsed[dst] ~= parsed_src[src] then
                    tables_used_as_new_value[dst] = true
                    return dst
                    -- the reference relation is changed...
                    -- perhaps: src's two field refer same tab but dst's fields refer different tables.
                    -- perhaps: dst's two field refer same tab but src's fields refer different tables.
                end
                if parsed[dst] then
                    -- this dst table is already checked. 
                    -- we keep the table and then try to remove it (if it's empty) later in trimEmptyTable
                    return dst
                end
                parsed[dst] = true
                parsed_src[src] = true
                local isarray = dst[1] ~= nil or not next(dst) and src[1] ~= nil
                if isarray then
                    local srccount = #src
                    local dstcount = #dst
                    local maxcount = math.max(srccount, dstcount)
                    for i = 1, maxcount do
                        dst[i] = extractDiff(src[i], dst[i])
                    end
                else
                    local dstkeys = {}
                    for k, v in pairs(dst) do
                        dst[k] = extractDiff(src[k], v)
                        dstkeys[k] = true
                    end
                    for k, v in pairs(src) do
                        if not dstkeys[k] then
                            dst[k] = "\024"
                        end
                    end
                end
                return dst
            else
                return dst
            end
        end
    end

    local trimed = {}
    local emptytabs = {}
    local function trimEmptyTable(tab)
        if type(tab) ~= "table" and not table.isudtable(tab) then
            return tab
        end

        if trimed[tab] then
            if emptytabs[tab] then
                return nil
            else
                return tab
            end
        end

        trimed[tab] = true
        for k, v in pairs(tab) do
            tab[k] = trimEmptyTable(v)
        end
        if next(tab) then
            return tab
        else
            if tables_used_as_new_value[tab] then
                return tab
            else
                emptytabs[tab] = true
                return nil
            end
        end
    end

    local fulldiff = extractDiff(src, diff)
    local trimeddiff = trimEmptyTable(fulldiff)
    return trimeddiff
end

-- this is a simple diff method. the dst must be plain data (without multi-reference to same table)
function lua2pb.diffSlim(src, dst)
    if src == dst then
        return nil
    end

    if dst == nil then
        return "\024"
    elseif type(dst) == "table" or table.isudtable(dst) then
        if type(src) ~= "table" and not table.isudtable(src) then
            return dst
        end

        local diff = {}
        local dstkeys = {}
        for k, v in pairs(dst) do
            diff[k] = lua2pb.diffSlim(src[k], v)
            dstkeys[k] = true
        end
        for k, v in pairs(src) do
            if not dstkeys[k] then
                diff[k] = "\024"
            end
        end
        if next(diff) then
            return diff
        else
            return nil
        end
    else
        return dst
    end
end

return lua2pb