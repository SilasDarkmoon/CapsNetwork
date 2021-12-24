local capsnetlua = class("capsnetlua")

capsnetlua.MAX_MESSAGE_LENGTH = 1024 * 1024
capsnetlua.PB_COMPOSER_USE_VARIANT_HEADER = true

local typeClrBytes
local function isClrBytes(val)
    if not typeClrBytes then
        typeClrBytes = clr.array(clr.System.Byte)
    end
    return rawequal(clr.type(val), typeClrBytes)
end

local pbformatter = class("pbformatter")

local preType2ID = clr.table(clr.Capstones.Net.PredefinedMessages.PredefinedTypeToID)
local preWriters = clr.table(clr.Capstones.Net.PredefinedMessages.PredefinedWriters)
local preReaders = clr.table(clr.Capstones.Net.PredefinedMessages.PredefinedReaders)

function pbformatter:PrepareBlock(data, type, flags, seq, sseq, exFlags)
    if data then
        local size = data.Count
        data.InsertMode = true
        data:Seek(0, clr.System.IO.SeekOrigin.Begin)
        local wrotecnt = 0
        local encoder = clr.Capstones.Net.ProtobufEncoder
        local pblltype = clr.Capstones.Net.ProtobufLowLevelType
        if capsnetlua.PB_COMPOSER_USE_VARIANT_HEADER then
            wrotecnt = wrotecnt + encoder.WriteTag(1, pblltype.Varint, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteVariant(encoder.EncodeZigZag32(clr.Capstones.Net.LuaNetProxyUtils.ConvertToInt(type)), data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteTag(2, pblltype.Varint, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteVariant(flags, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteTag(3, pblltype.Varint, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteVariant(seq, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteTag(4, pblltype.Varint, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteVariant(sseq, data, wrotecnt);
        else
            wrotecnt = wrotecnt + encoder.WriteTag(1, pblltype.Fixed32, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteFixed32(type, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteTag(2, pblltype.Fixed32, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteFixed32(flags, data, wrotecnt)
            wrotecnt = wrotecnt + encoder.WriteTag(3, pblltype.Fixed32, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteFixed32(seq, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteTag(4, pblltype.Fixed32, data, wrotecnt);
            wrotecnt = wrotecnt + encoder.WriteFixed32(sseq, data, wrotecnt);
        end
        wrotecnt = wrotecnt + encoder.WriteTag(5, pblltype.LengthDelimited, data, wrotecnt);
        wrotecnt = wrotecnt + encoder.WriteVariant(size, data, wrotecnt);
    end
end

function pbformatter.GetMessageName2IDMap()
    if not pbformatter.pbmap_name2id then
        pbformatter.pbmap_name2id = {}
        pbformatter.pbmap_id2name = {}

        local pb = require("pb")
        if type(___CONFIG__NETLUA_PB_MAP_INIT) == "function" then
            ___CONFIG__NETLUA_PB_MAP_INIT()
        end
        for name, basename, type in pb.types() do
            local fullname = name
            if string.sub(name, 1, 1) == "." then
                fullname = string.sub(name, 2, -1)
            end
        
            local hash = clr.Capstones.UnityEngineEx.ExtendedStringHash.GetHashCodeExShort(clr.Capstones.UnityEngineEx.ExtendedStringHash.GetHashCodeEx(fullname))

            pbformatter.pbmap_name2id[fullname] = hash
            pbformatter.pbmap_id2name[hash] = fullname
        end
    end

    return pbformatter.pbmap_name2id, pbformatter.pbmap_id2name
end

function pbformatter:GetDataType(data)
    if not data then
        return 0
    end

    local clrtype = clr.type(data)

    if clrtype == clr.Capstones.Net.PredefinedMessages.Unknown then
        return data.TypeID
    end

    local preid = preType2ID[clrtype]
    if preid and preid ~= 0 then
        return preid
    end

    if data.messageName then
        local map_n2i = pbformatter.GetMessageName2IDMap()
        local pbid = map_n2i[data.messageName]
        if pbid then
            return pbid
        end
    end

    return 0
end

function pbformatter:CanWrite(data)
    if data then
        local clrtype = clr.type(data)
        if clrtype == clr.Capstones.Net.PredefinedMessages.Unknown then
            return true
        end

        local preid = preType2ID[clrtype]
        if preid and preid ~= 0 then
            return true
        end
    end

    if data.messageName then
        local map_n2i = pbformatter.GetMessageName2IDMap()
        local pbid = map_n2i[data.messageName]
        if pbid then
            return true
        end
    end

    return false
end

function pbformatter:IsOrdered(data)
    return self:CanWrite(data)
end

function pbformatter:Write(data)
    if data then
        local clrtype = clr.type(data)
        local writer = preWriters[clrtype]
        if writer then
            return writer(data)
        end
    end

    if data.messageName then
        if not pbformatter.write_UnderlayStream then
            pbformatter.write_UnderlayStream = clr.Capstones.UnityEngineEx.ArrayBufferStream()
        end
        local stream = pbformatter.write_UnderlayStream
        stream:Clear()
        local raw = pb.encode(data.messageName, data)
        stream:Write(raw, 0, #raw)
        return stream
    end

    return nil
end

function pbformatter:Read(type, buffer, offset, cnt, exFlags)
    local prereader = preReaders[type]
    if prereader then
        return prereader(type, buffer, offset, cnt)
    end

    local mapn2i, mapi2n = pbformatter.GetMessageName2IDMap()
    local messageName = mapi2n[type]
    if messageName then
        local raw = clr.array(cnt, clr.System.Byte)
        buffer:Seek(offset, clr.System.IO.SeekOrigin.Begin)
        buffer:Read(raw, 0, cnt)
        local message = pb.decode(messageName, raw)
        message.messageName = messageName
        return message
    end
end

---------------------------------------------------------------------------

local function ConvertMessage(data)
    if clr.type(data) == clr.Capstones.Net.CarbonMessage then
        return {
            __iscarbon = true,
            flags = data.Flags,
            cate = data.Cate,
            type = data.Type,
            message = data.ObjMessage,
        }
    end
    return data
end

local function ConvertMessageBack(data)
    if type(data) == "table" and data.__iscarbon then
        local mess = clr.Capstones.Net.CarbonMessage()
        mess.Flags = data.flags
        mess.Cate = data.cate
        mess.Type = data.type
        mess.ObjMessage = data.message
        return mess
    end
    return data
end

function capsnetlua.Init()
    capsnetlua.instance = capsnetlua.new()
end

function capsnetlua:ctor()
    self.readContext = { size = 0 }
    self.pbformatter = pbformatter.new()
end

function capsnetlua:Dispose()
end

function capsnetlua:FireReceiveBlock(splitter, buffer, size, type, flags, seq, sseq, exFlags)
    self:ResetReadBlockContext()
    splitter:FireReceiveBlockBase(buffer, size, type, flags, seq, sseq, exFlags)
end

function capsnetlua:OnLuaReceiveBlock(splitter, buffer, size, exFlags)
    if not buffer then
        self:FireReceiveBlock(splitter, nil, 0, exFlags.type, exFlags.flags, 0, 0, exFlags)
    elseif exFlags.cate == 2 and exFlags.type == 10001 then
        local decodeSuccess = false
        local seq_client = 0
        local sseq = 0
        local pbtype = 0
        local pbtag = 0
        local pbflags = 0
        local pbsize = 0

        buffer:Seek(0, clr.System.IO.SeekOrigin.Begin)
        local function TryParseBlock()
            while true do
                -- Read Each Tag-Field
                if pbtype == 0 then
                    -- Determine the start of a message.
                    while pbtag == 0 do
                        pbtag = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                    end
                else
                    -- The Next tag must follow
                    pbtag = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                    if pbtag == 0 then
                        return
                    end
                end
                -- Tag got.
                local seq = clr.Google.Protobuf.WireFormat.GetTagFieldNumber(pbtag)
                local ttype = clr.Google.Protobuf.WireFormat.GetTagWireType(pbtag)
                if seq == 1 then
                    if ttype == clr.Google.Protobuf.WireFormat.WireType.Varint then
                        clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                        local value = clr.lastulong()
                        pbtype = clr.Capstones.Net.LuaNetProxyUtils.ConvertToUInt(clr.Capstones.Net.ProtobufEncoder.DecodeZigZag64(value))
                    elseif ttype == clr.Google.Protobuf.WireFormat.WireType.Fixed32 then
                        pbtype = clr.Capstones.Net.ProtobufEncoder.ReadFixed32(buffer)
                    end
                elseif pbtype ~= 0 then
                    if seq == 2 then
                        if ttype == clr.Google.Protobuf.WireFormat.WireType.Varint then
                            pbflags = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                        elseif ttype == clr.Google.Protobuf.WireFormat.WireType.Fixed32 then
                            pbflags = clr.Capstones.Net.ProtobufEncoder.ReadFixed32(buffer)
                        end
                    elseif seq == 3 then
                        if ttype == clr.Google.Protobuf.WireFormat.WireType.Varint then
                            seq_client = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                        elseif ttype == clr.Google.Protobuf.WireFormat.WireType.Fixed32 then
                            seq_client = clr.Capstones.Net.ProtobufEncoder.ReadFixed32(buffer)
                        end
                    elseif seq == 4 then
                        if ttype == clr.Google.Protobuf.WireFormat.WireType.Varint then
                            sseq = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                        elseif ttype == clr.Google.Protobuf.WireFormat.WireType.Fixed32 then
                            sseq = clr.Capstones.Net.ProtobufEncoder.ReadFixed32(buffer)
                        end
                    elseif seq == 5 then
                        if ttype == clr.Google.Protobuf.WireFormat.WireType.LengthDelimited then
                            pbsize = clr.Capstones.Net.ProtobufEncoder.ReadVariant(buffer)
                        elseif ttype == clr.Google.Protobuf.WireFormat.WireType.Fixed32 then
                            pbsize = clr.Capstones.Net.ProtobufEncoder.ReadFixed32(buffer)
                        else
                            pbsize = 0
                        end
                        if pbsize >= 0 then
                            if pbsize > buffer.Length - buffer.Position then
                                printe("We got a too long message. We will drop this message and treat it as an error message.")
                                return
                            else
                                buffer:Consume()
                                self:FireReceiveBlock(splitter, buffer, pbsize, pbtype, pbflags, seq_client, sseq, exFlags)
                                decodeSuccess = true
                                return
                            end
                        end
                    end
                end
                pbtag = 0
            end
        end
        xpcall(TryParseBlock, printe)
        if not decodeSuccess then
            self:FireReceiveBlock(splitter, nil, 0, pbtype, pbflags, seq_client, sseq, exFlags)
        end
    else
        self:FireReceiveBlock(splitter, buffer, size, exFlags.type, exFlags.flags, 0, 0, exFlags)
    end
end

function capsnetlua:ResetReadBlockContext()
    self.readContext.size = 0
    self.readContext.exFlags = nil
end

function capsnetlua:ReadHeaders(splitter)
    local stream = splitter.InputStream
    -- Read size.(4 bytes)
    local size = clr.Capstones.Net.LuaNetProxyUtils.ReadUIntBigEndian(stream)
    -- Read flags.(2 byte)
    local flags = clr.Capstones.Net.LuaNetProxyUtils.ReadShortBigEndian(stream)
    -- Read Cate.(2 byte)
    local cate = clr.Capstones.Net.LuaNetProxyUtils.ReadShortBigEndian(stream)
    -- Read EndPoint (8 byte)
    clr.Capstones.Net.LuaNetProxyUtils.ReadLongBigEndian(stream)
    local endpoint = clr.lastlong()
    -- Read Type. (4 byte)
    local type = clr.Capstones.Net.LuaNetProxyUtils.ReadIntBigEndian(stream)

    return size, flags, cate, type, endpoint
end

function capsnetlua:ReadBlock(splitter)
    local stream = splitter.InputStream
    local buffer = splitter.ReadBuffer

    local size, flags, cate, type, endpoint = self:ReadHeaders(splitter)
    local realsize = size - 16
    local exFlags = {
        flags = flags,
        cate = cate,
        type = type,
        endpoint = endpoint,
    }
    if realsize >= 0 then
        if realsize > capsnetlua.MAX_MESSAGE_LENGTH then
            printe("We got a too long message. We will drop this message and treat it as an error message.")
            clr.Capstones.Net.ProtobufEncoder.SkipBytes(stream, realsize)
            self:OnLuaReceiveBlock(splitter, nil, 0, exFlags)
        else
            buffer:Clear()
            clr.Capstones.Net.ProtobufEncoder.CopyBytes(stream, buffer, realsize)
            self:OnLuaReceiveBlock(splitter, buffer, realsize, exFlags)
        end
    else
        self:OnLuaReceiveBlock(splitter, nil, 0, exFlags)
    end
    self:ResetReadBlockContext()
end

function capsnetlua:TryReadBlock(splitter)
    local stream = splitter.InputStream
    local buffer = splitter.ReadBuffer

    while true do
        if not self.readContext.exFlags then
            if splitter.BufferedSize < 20 then
                return false
            end
            local size, flags, cate, type, endpoint = self:ReadHeaders(splitter)
            local realsize = size - 16
            local exFlags = {
                flags = flags,
                cate = cate,
                type = type,
                endpoint = endpoint,
            }
            self.readContext.exFlags = exFlags
            self.readContext.size = realsize
        else
            if self.readContext.size >= 0 then
                if splitter.BufferedSize < self.readContext.size then
                    return false
                end
                if self.readContext.size > capsnetlua.MAX_MESSAGE_LENGTH then
                    printe("We got a too long message. We will drop this message and treat it as an error message.")
                    clr.Capstones.Net.ProtobufEncoder.SkipBytes(stream, self.readContext.size)
                    self:OnLuaReceiveBlock(splitter, nil, 0, self.readContext.exFlags)
                else
                    buffer:Clear()
                    clr.Capstones.Net.ProtobufEncoder.CopyBytes(stream, buffer, self.readContext.size)
                    self:OnLuaReceiveBlock(splitter, buffer, self.readContext.size, self.readContext.exFlags)
                end
            else
                self:OnLuaReceiveBlock(splitter, nil, 0, self.readContext.exFlags)
            end
            self:ResetReadBlockContext()
            return true
        end
    end
end

function capsnetlua:PrepareBlock(data, type, flags, seq, sseq, exFlags)
    if data then
        if exFlags and exFlags.cate == 2 and exFlags.type == 10001 then
            -- Wrapped-Protobuf
            self.pbformatter:PrepareBlock(data, type, flags, seq, sseq, exFlags)
        end

        local size = data.Count
        data.InsertMode = true
        data:Seek(0, clr.System.IO.SeekOrigin.Begin)

        local carbonFlags = 0
        local carbonCate = 0
        local carbonType = 0
        local carbonEndpoint = 0
        if exFlags then
            carbonFlags = exFlags.flags
            carbonCate = exFlags.cate
            carbonType = exFlags.type
            carbonEndpoint = exFlags.endpoint
        end

        local full_size = size + 16

        -- write size.(4 bytes) (not included in full_size)
        clr.Capstones.Net.LuaNetProxyUtils.WriteUIntBigEndian(data, full_size)
        -- write flags.(2 byte)
        clr.Capstones.Net.LuaNetProxyUtils.WriteShortBigEndian(data, carbonFlags)
        -- Write Cate.(2 byte)
        clr.Capstones.Net.LuaNetProxyUtils.WriteShortBigEndian(data, carbonCate)
        -- Write EndPoint (8 byte)
        clr.Capstones.Net.LuaNetProxyUtils.WriteLongBigEndian(data, carbonEndpoint)
        -- Write Type.(4 bytes)
        clr.Capstones.Net.LuaNetProxyUtils.WriteIntBigEndian(data, carbonType)
    end
end

function capsnetlua:GetExFlags(data)
    data = ConvertMessage(data)
    if type(data) == "table" and data.__iscarbon then
        return {
            flags = data.flags,
            cate = data.cate,
            type = data.type,
            endpoint = data.endpoint,
        }
    elseif isClrBytes(data) then
        return {
            flags = 0,
            cate = 2,
            type = -128,
            endpoint = 0,
        }
    elseif clr.type(data) == clr.System.String then
        return {
            flags = 0,
            cate = 1,
            type = -128,
            endpoint = 0,
        }
    elseif self:GetDataType(data) ~= 0 then
        return {
            flags = 0,
            cate = 2,
            type = 10001,
            endpoint = self.wrappedEndPoint or 0,
        }
    elseif clr.is(data, clr.Google.Protobuf.IMessage) then
        return {
            flags = 0,
            cate = 4,
            type = -128,
            endpoint = 0,
        }
    else
        return {
            flags = 0,
            cate = 2,
            type = -128,
            endpoint = 0,
        }
    end
end

function capsnetlua:GetDataType(data)
    if type(data) == "table" and data.__iscarbon then
        if data.cate == 2 and data.type == 10001 then
            return self.pbformatter:GetDataType(data.message)
        else
            return 0
        end
    else
        return self.pbformatter:GetDataType(data)
    end
end

function capsnetlua:CanWrite(data)
    data = ConvertMessage(data)
    if type(data) == "table" and data.__iscarbon then
        return true
    else
        return self.pbformatter:CanWrite(data)
    end
end

function capsnetlua:IsOrdered(data)
    data = ConvertMessage(data)
    if type(data) == "table" and data.__iscarbon then
        return false
    else
        return self.pbformatter:IsOrdered(data)
    end
end

function capsnetlua:Write(data)
    data = ConvertMessage(data)
    if type(data) == "table" and data.__iscarbon then
        if data.message then
            return self.pbformatter:Write(data.message)
        else
            return self.pbformatter:Write(clr.Capstones.Net.PredefinedMessages.Empty)
        end
    else
        return self.pbformatter:Write(data)
    end
end

function capsnetlua:Read(type, buffer, offset, cnt, exFlags)
    if not exFlags then
        return self.pbformatter:Read(type, buffer, offset, cnt, exFlags)
    else
        local carbon = {
            __iscarbon = true,
            flags = exFlags.flags,
            cate = exFlags.cate,
            type = exFlags.type,
        }

        if exFlags.cate == 3 or exFlags.cate == 1 then
            -- Json
            local raw = clr.array(cnt, clr.System.Byte)
            buffer:Seek(offset, clr.System.IO.SeekOrigin.Begin)
            buffer:Read(raw, 0, cnt)
            local str = clr.unwrap(raw)
            carbon.message = str
        elseif exFlags.cate == 4 then
            -- PB
            carbon.message = clr.Capstones.Net.ProtobufEncoder.ReadRaw(buffer, offset, cnt)
        elseif exFlags.cate == 2 and exFlags.type == 10001 then
            -- Wrapped typed PB
            carbon.message = self.pbformatter:Read(type, buffer, offset, cnt, exFlags)
            return carbon.message
        else
            -- Raw
            local raw = clr.array(cnt, clr.System.Byte)
            buffer:Seek(offset, clr.System.IO.SeekOrigin.Begin)
            buffer:Read(raw, 0, cnt)
            carbon.message = raw
        end
        return ConvertMessageBack(carbon)
    end
end

___LuaNet__Init = capsnetlua.Init
___LuaNet__Dispose = function()
    if capsnetlua.instance then
        capsnetlua.instance:Dispose()
        capsnetlua.instance = nil
    end
end
___LuaNet__FireReceiveBlock = function(...)
    capsnetlua.instance:FireReceiveBlock(...)
end
___LuaNet__ReadBlock = function(...)
    capsnetlua.instance:ReadBlock(...)
end
___LuaNet__TryReadBlock = function(...)
    return capsnetlua.instance:TryReadBlock(...)
end
___LuaNet__PrepareBlock = function(...)
    capsnetlua.instance:PrepareBlock(...)
end
___LuaNet__GetExFlags = function(...)
    return capsnetlua.instance:GetExFlags(...)
end
___LuaNet__GetDataType = function(...)
    return capsnetlua.instance:GetDataType(...)
end
___LuaNet__CanWrite = function(...)
    return capsnetlua.instance:CanWrite(...)
end
___LuaNet__IsOrdered = function(...)
    return capsnetlua.instance:IsOrdered(...)
end
___LuaNet__Write = function(...)
    return capsnetlua.instance:Write(...)
end
___LuaNet__Read = function(...)
    return capsnetlua.instance:Read(...)
end

----------------------------------------------------------------
package.loaded["capsnetlua"] = capsnetlua

capsnetlua.ConnectionConfig = clr.Capstones.Net.ConnectionFactory.ConnectionConfig(clr.Capstones.Net.CarbonMessageUtils.ConnectionConfig)
local sconfig = clr.Capstones.Net.SerializationConfig()
sconfig.SplitterFactory = clr.Capstones.Net.LuaSplitter.Factory
sconfig.Composer = clr.Capstones.Net.LuaComposer()
sconfig.FormatterFactory = clr.Capstones.Net.LuaFormatter.Factory
capsnetlua.ConnectionConfig.SConfig = sconfig

function capsnetlua.Connect(url)
    capsnetlua.CarbonPushConnection = clr.Capstones.Net.ConnectionFactory.GetClient(url, capsnetlua.ConnectionConfig)
    return capsnetlua.CarbonPushConnection
end
function capsnetlua.ConnectToHostAndPort(host, port)
    local url = clr.Capstones.Net.CarbonMessageUtils.CombineUrl(host, port)
    return capsnetlua.Connect(url)
end
function capsnetlua.ConnectWithDifferentPort(url, port)
    local uri = clr.System.Uri(url)
    return capsnetlua.ConnectToHostAndPort(uri.DnsSafeHost, port)
end

return capsnetlua