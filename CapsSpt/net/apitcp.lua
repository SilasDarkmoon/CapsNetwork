local CarbonMessageUtils = clr.Capstones.Net.CarbonMessageUtils
local capsnetlua = require("capsnetlua")
local concreator = capsnetlua
--___DEBUG__USE_LEGACY_TCPAPI = true
if ___DEBUG__USE_LEGACY_TCPAPI then
    concreator = CarbonMessageUtils
end
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
local reconnectCount = 0

local function OnTcpMessage(message, messagetype)
    if api.forbidTcpMessage then
        return
    end
    reconnectCount = 0
    local jsonMess, err = json.decode(message)
    if jsonMess then
        message = jsonMess
    else
        printe(err)
    end
    dump({ type = messagetype, message = message }, "recv")
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

function api.TcpReconnect()
    print("retry tcp connect: "..reconnectCount)
    if reconnectCount >= 3 then
        clr.coroutine(function()
            local waithandle = req.defaultOnFailed({ failed = "tcp_retry", msg = clr.transstr("tcp_retry") })
            if waithandle then
                while not waithandle.done do
                    coroutine.yield()
                end
            end
            reconnectCount = 0
            api.TcpConnect()
        end)
    else
        reconnectCount = reconnectCount + 1
        api.TcpConnect()
    end
end

local connectDelayedChecking = nil
local function ConnectDelayedCheck()
    if connectDelayedChecking then
        return
    end
    connectDelayedChecking = true
    unity.async(function()
        unity.await.rtime(5)
        connectDelayedChecking = nil
        if not api.tcpClient or api.tcpClient == clr.null or not api.tcpClient.IsAlive then
            api.TcpReconnect()
        end
    end)
end

function api.TcpConnect()
    local token = api.token
    if not token or token == "" then
        return
    end

    api.CloseTcpConnect()
    if ___CONFIG__TCP_URL then
        api.tcpClient = concreator.Connect(___CONFIG__TCP_URL)
    else
        if not api.tcpHost then
            api.tcpHost = ___CONFIG__BASE_URL
        end
        if not api.tcpPort then
            api.tcpPort = 8976
        end
        api.tcpClient = concreator.ConnectWithDifferentPort(api.tcpHost, api.tcpPort)
    end
    CarbonMessageUtils.OnClose(api.tcpClient, api.TcpReconnect)
    CarbonMessageUtils.OnJson(api.tcpClient, OnTcpMessage)
    CarbonMessageUtils.SendToken(api.tcpClient, token)

    if api.OnTcpConnected then
        api.OnTcpConnected()
    end
    ConnectDelayedCheck()
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