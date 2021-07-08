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
                    if validKeyTypes[kt] and validValTypes[vt] then
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
-- if a table is not an array, it will be like { i = id, t = "o", k = { 1,3,5 }, v = { v1, v2, v3} }. The 1,3,5 in 'k' means the field key is the 1st/3rd/5th string in the global key array.
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

    if type(tab) == "table" then
        return {
            Keys = tab.k,
            Val = convertValue(tab.v),
        }
    else
        return convertValue(tab)
    end
end

function lua2pb.encode(tab)
    local data = lua2pb.extractDataFromTable(tab)
    local plain = lua2pb.convertDataTableToPlain(data)
    local compact = lua2pb.convertPlainDataToCompact(plain)
    local ptab = lua2pb.convertCompactDataToProtobuf(compact)

    local raw = pb.encode("protocols.CompactLuaValueProto", ptab)
    return raw
end

return lua2pb