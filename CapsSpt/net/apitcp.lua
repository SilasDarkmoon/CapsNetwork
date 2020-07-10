local CarbonMessageUtils = clr.Capstones.Net.CarbonMessageUtils

--api.tcpHost = nil
--api.tcpPort = nil
--api.tcpClient = nil

local oldSetToken = api.setToken
function api.setToken(token)
    local changed = api.token ~= token
    oldSetToken(token)
    if changed then
        api.TcpConnect()
    end
end

local typedMessageHandlers = {}
local cachedTypedMessage = {}

local function OnTcpMessage(message, messagetype)
    local jsonMess, err = json.decode(message)
    if jsonMess then
        message = jsonMess
    else
        printe(err)
    end
    ndump(messagetype, message)
    local typedHandler = typedMessageHandlers[messagetype]
    if type(typedHandler) == "function" then
        typedHandler(message, messagetype)
    else
        cachedTypedMessage[messagetype] = message
    end
    if type(api.OnTcpMessage) == "function" then
        api.OnTcpMessage(message, messagetype)
    end
end

function api.CloseTcpConnect()
    if api.tcpClient and api.tcpClient ~= clr.null then
        CarbonMessageUtils.OnClose(api.tcpClient, nil)
        api.tcpClient:Dispose()
        api.tcpClient = nil
    end
end

function api.TcpConnect()
    local token = api.token
    if not token or token == "" then
        return
    end

    if not api.tcpHost then
        api.tcpHost = ___CONFIG__BASE_URL
    end
    if not api.tcpPort then
        api.tcpPort = 8976
    end
    api.CloseTcpConnect()
    api.tcpClient = CarbonMessageUtils.ConnectWithDifferentPort(api.tcpHost, api.tcpPort)
    CarbonMessageUtils.OnClose(api.tcpClient, api.TcpConnect)
    CarbonMessageUtils.OnJson(api.tcpClient, OnTcpMessage)
    CarbonMessageUtils.SendToken(api.tcpClient, token)
end

function api.RegTcpMessageHandler(handler, messagetype)
    if messagetype then
        typedMessageHandlers[messagetype] = handler
        local cachedMessage = cachedTypedMessage[messagetype]
        if type(handler) == "function" and cachedMessage then
            handler(cachedMessage, messagetype)
            cachedTypedMessage[messagetype] = nil
        end
    else
        api.OnTcpMessage = handler
    end
end

return api