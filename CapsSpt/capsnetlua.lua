local capsnetlua = class("capsnetlua")

capsnetlua.MAX_MESSAGE_LENGTH = 1024 * 1024

function capsnetlua.Init()
    capsnetlua.instance = capsnetlua.new()
end

function capsnetlua:ctor()
    self.readContext = { size = 0 }
    self.pbformatter = clr.Capstones.Net.ProtobufComposer()
end

function capsnetlua:Dispose()
end

function capsnetlua:FireReceiveBlock(splitter, buffer, size, type, flags, seq, sseq, exFlags)
    self:ResetReadBlockContext()
    splitter:FireReceiveBlockPublic(buffer, size, type, flags, seq, sseq, exFlags)
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
                        pbtype = clr.Capstones.Net.ProtobufEncoder.DecodeZigZag64(value)
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
            self:FireReceiveBlock(splitter, null, 0, pbtype, pbflags, seq_client, sseq, exFlags)
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

local typeClrBytes
local function isClrBytes(val)
    if not typeClrBytes then
        typeClrBytes = clr.array(clr.System.Byte)
    end
    return rawequal(clr.type(val), typeClrBytes)
end

function capsnetlua:GetExFlags(data)
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
    if type(data) == "table" and data.__iscarbon then
        return true
    else
        return self.pbformatter:CanWrite(data)
    end
end

function capsnetlua:IsOrdered(data)
    if type(data) == "table" and data.__iscarbon then
        return false
    else
        return self.pbformatter:IsOrdered(data)
    end
end

function capsnetlua:Write(data)
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
            local str = clr.System.Text.Encoding.UTF8.GetString(raw, 0, cnt)
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
        return carbon
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

return capsnetlua