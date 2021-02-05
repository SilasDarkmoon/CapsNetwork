api = {}

local UnityEngine = clr.UnityEngine
local Object = UnityEngine.Object
local unpack = unpack or table.unpack

function api.setToken(token)
    api.token = token
end

function api.getToken()
    return api.token
end

function api.normalizeUrl(uri)
    -- TODO:
    -- 1. make lower;
    -- 2. if starts with "http://" check starts with url.baseUrl;
    -- 3. the part with out "http://", change // to /
    -- 4. the part relative to url.baseUrl, remove starting /
    if string.sub(uri, 1, string.len("http://")) ~= "http://" and string.sub(uri, 1, string.len("https://")) ~= "https://" then
        uri = tostring(___CONFIG__BASE_URL or "") .. uri
    end
    return uri
end

local nextRequestSeq = 1
local nextRealSeq = 1
local repostReq -- function
local restartReq -- function

local function createRequest(uri, data, seq, timeOut)
    local datamt = getmetatable(data)
    local www
    local request
    -- TODO:扩展创建请求时候设置超时
    local wwwTimeout = api.timeout
    if timeOut ~= nil and type(timeOut) == "number" and timeOut > 0 then
        wwwTimeout = timeOut
    end

    if datamt and datamt.rawpost then
        -- TODO:是对外部服务器的请求
        local rawdata = data
        local form = clr.Capstones.Net.HttpRequestData()
        local headers = clr.Capstones.Net.HttpRequestData()
        www = clr.Capstones.Net.HttpRequestBase.Create(uri, headers, form, nil)
        www.Timeout = wwwTimeout * 1000
        -----------------------------------
        form.PrepareMethod = "json"
        form:Add("", json.encode(data))

        www:StartRequest()
        request = {}
        setmetatable(request, {__isobject = true})
        request.www = www
        request.uri = uri
        request.pdata = rawdata
        request.token = api.token
        request.seq = seq
        request.repost = repostReq
        request.restart = restartReq
    else
        if type(seq) ~= 'number' then
            seq = nextRequestSeq
            nextRequestSeq = nextRequestSeq + 1
        end
        local rseq = nextRealSeq
        nextRealSeq = nextRealSeq + 1

        local rawdata = data
        -- local fulldata = { d = data }
        -- local fulldata = { data }
        local fulldata = data
        -- data = json.encode(data)
        local form = clr.Capstones.Net.HttpRequestData()
        local headers = clr.Capstones.Net.HttpRequestData()
        www = clr.Capstones.Net.HttpRequestBase.Create(uri, headers, form, nil)

        if api.token then
            fulldata.t = tostring(api.token)
            www.Token = fulldata.t
        end

        -- fulldata.seq = tostring(seq)
        -- www.Seq = fulldata.seq
        www.Seq = tostring(seq)

        -- fulldata.rseq = tostring(rseq)
        -- www.RSeq = fulldata.rseq
        www.RSeq = tostring(rseq)

        form.PrepareMethod = "json"
        form:Add("", json.encode(fulldata))

        www.Timeout = wwwTimeout * 1000

        www:StartRequest()
        request = {}
        setmetatable(request, {__isobject = true})
        request.www = www
        request.uri = uri
        request.pdata = rawdata
        request.token = api.token
        request.seq = seq
        request.repost = repostReq
        request.restart = restartReq
    end
    clr.coroutine(function()
        coroutine.yield()
        local error, done
        while not done do
            if clr.isobj(www) then
                local isMyTimedout = nil
                local startTime = UnityEngine.Time.realtimeSinceStartup
                while true do
                    if www.IsDone then
                        break
                    end
                    unity.waitForNextEndOfFrame()
                    if UnityEngine.Time.realtimeSinceStartup - startTime > (wwwTimeout + 2) then
                        dumpe("createRequest =====> 等待超时")
                        isMyTimedout = true
                        break
                    end
                end
                api.result(request,isMyTimedout)
                if request.failed or isMyTimedout == true then
                    error = true
                    done = true
                else
                    error = nil
                    done = true
                end
            else
                error = true
                done = true
            end
        end
        if type(request.doneFuncs) == 'table' then
            if error then
                if type(request.doneFuncs.onFailed) == 'function' then
                    request.doneFuncs.onFailed(request)
                end
            else
                if type(request.doneFuncs.onComplete) == 'function' then
                    request.doneFuncs.onComplete(request)
                end
            end
            if type(request.doneFuncs.onDone) == 'function' then
                request.doneFuncs.onDone(request)
            end
        end
    end)

    return request
end

function restartReq(request) -- local
    request.www:StopRequest()
    local request2 = createRequest(request.uri, request.pdata, request.seq, request.timeOut)
    request2.quiet = request.quiet
    request2.blockdlg = request.blockdlg
    request2.doneFuncs = request.doneFuncs

    return request2
end

function repostReq(request) -- local
    local request2 = restartReq(request)
    local quiet = request.quiet

    if request2.blockdlg then
        local block = request2.blockdlg
        request2.blockdlg = nil
        clr.coroutine(function()
            unity.waitForNextEndOfFrame()
            Object.Destroy(block)
        end)
    end
    if not quiet then
        if api.showBlockDialog then
            request2.blockdlg = api.showBlockDialog()
        end
    end

    return request2
end

function api.post(uri, data, quiet, timeOut)
    uri = api.normalizeUrl(uri)
    local label = "Request = " .. nextRequestSeq .. ": " .. uri .. "\nData"
    dump(data, label)

    local request = createRequest(uri, data, nil, timeOut)
    request.quiet = quiet

    if not quiet then
        if api.showBlockDialog then
            request.blockdlg = api.showBlockDialog()
        end
    end

    return request
end

api.timeout = 20
-- TODO: 1、转菊花的prefab
function api.wait(request, onComplete, onFailed)
    local done = nil
    request.doneFuncs = {
        onFailed = onFailed,
        onComplete = onComplete,
        onDone = function(realRequest) done = realRequest end,
    }

    while not done do
        unity.waitForNextEndOfFrame()
    end

    return done
end

function api.postwait(uri, data, onComplete, onFailed, quiet, timeOut)
    return api.wait(api.post(uri, data, quiet, timeOut), onComplete, onFailed)
end

function api.waitany(...)
    local done = nil

    local tab = cache.totable(...)
    for k, v in pairs(tab) do
        if clr.isobj(v) or type(v) == 'table' and getmetatable(v) and clr.isobj(getmetatable(v).__index) then
            if type(v.doneFuncs) ~= 'table' then
                v.doneFuncs = {}
            end
            v.doneFuncs.onDone = function(realRequest) done = realRequest end
        end
    end

    while not done do
        unity.waitForNextEndOfFrame()
    end

    return done
end

function api.waitall(...)
    local undone = {}
    local done = {}
    local tab = cache.totable(...)
    for k, v in pairs(tab) do
        if clr.isobj(v) or type(v) == 'table' and getmetatable(v) and clr.isobj(getmetatable(v).__index) then
            undone[k] = v
            if type(v.doneFuncs) ~= 'table' then
                v.doneFuncs = {}
            end
            v.doneFuncs.onDone = function(realRequest)
                undone[k] = nil
                done[k] = realRequest
            end
        end
    end

    while next(undone) do
        unity.waitForNextEndOfFrame()
    end

    if tab == select(1, ...) then
        return done
    else
        return unpack(done, 1, select('#', ...))
    end
end

function api.bool(val)
    return val and val ~= '' and val ~= 0
end

function api.result(request,isMyTimedout)
    if request.www.IsDone or isMyTimedout == true then
        if not request.done or isMyTimedout == true then
            request.done = true
            local failed, msg, event = false, nil, nil
            local error = request.www.Error
            if error == 'timedout' or isMyTimedout == true then
                failed = 'timedout'
                event = "none"
                msg = clr.transstr('timedOut')
            elseif api.bool(error) then
                failed = 'network'
                event = "none"
                if type(error) == "string" then
                    msg = error
                    if string.sub(error, 1, 11) == "HttpError: " then
                        local sub = string.sub(error, 12)
                        local split = string.find(sub, "\n", 1, true)
                        if split then
                            msg = string.sub(sub, split + 1)
                            sub = string.sub(sub, 1, split - 1)
                        end
                        local code = tonumber(sub)
                        if code and code ~= 0 then
                            if ___CONFIG__HTTPERROR_MSG and ___CONFIG__HTTPERROR_MSG[code] then
                                msg = clr.transstr(___CONFIG__HTTPERROR_MSG[code])
                            end
                            failed = code
                        else
                            failed = sub
                        end
                    end
                -- else
                --     msg = tostring(error)
                end
                if not msg then
                    msg = clr.transstr('networkError')
                end
            else
                msg = request.www:ParseResponseText(request.token, request.seq)
                local tab = json.decode(msg)
                if type(tab) ~= 'table' then
                    failed = true
                    event = "none"
                    msg = clr.transstr(msg)
                else
                    dump(tab)
                    local datamt = getmetatable(request.pdata)
                    if datamt and datamt.rawpost then
                        request.val = tab
                        msg = tab and clr.transstr(tab.tips) or clr.transstr('server_refuse', failed)
                    else
                        local type = tab.type and tonumber(tab.type)
                        if type and type <= 0 then
                            if type == 0 then
                                failed = true
                            else
                                failed = type
                            end
                            msg = clr.transstr(tab.tips) or tab.tips or clr.transstr('server_refuse', failed)
                            -- msg = msg .. "\n" .. tab.traceIdentifier
                        end
                        request.val = tab.d
                        request.event = tab.e
                    end
                end
            end
            request.msg = msg
            if failed then
                dump(msg)
                if event then
                    request.event = event
                end
                request.failed = failed
                request.success = false
            else
                request.failed = false
                request.success = true
            end
            request.www:StopRequest()
            if request.blockdlg then
                local block = request.blockdlg
                request.blockdlg = nil
                clr.coroutine(function()
                    unity.waitForNextEndOfFrame()
                    Object.Destroy(block)
                end)
            end
        end
    end
    return request
end

local function success(request)
    return request.done and request.success
end

function api.success(...)
    local s = true
    cache.foreach(function(request)
        if not success(request) then
            s = false
            return 'break'
        end
    end, ...)
    return s
end

function api.failed(...)
    local failed
    cache.foreach(function(request)
        if not success(request) then
            failed = request
            return 'break'
        end
    end, ...)
    return failed
end

function api.msg(request)
    if not (type(request) == 'table' and request.www and clr.isobj(request.www)) then
        return clr.transstr('invalid_request_obj')
    end
    if not request.done then
        return clr.transstr('request_not_completed')
    end
    return request.msg
end

return api
