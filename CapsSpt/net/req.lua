req = { }
url = { }

reqDefaultListeners = { }
reqDefaultErrorListeners = { }
reqEventListeners = { }
reqResultPrepareListeners = { }
reqResultPrepareEventListeners = { }
reqRequestDataPrepareListeners = { }

local reqEventListenersStacks = cache.setValueWithHistoryAndCategory()
function reqEventListenersStacks.onGetValue(cate)
    return reqEventListeners[cate]
end
function reqEventListenersStacks.onSetValue(cate, val)
    reqEventListeners[cate] = val
end
req.getEventListener = reqEventListenersStacks.getValue
req.regEventListener = reqEventListenersStacks.pushValue
req.popEventListener = reqEventListenersStacks.popValue

local reqResultPrepareListenersStacks = cache.setValueWithHistoryAndCategory()
function reqResultPrepareListenersStacks.onGetValue(cate)
    return reqResultPrepareListeners[cate]
end
function reqResultPrepareListenersStacks.onSetValue(cate, val)
    reqResultPrepareListeners[cate] = val
end
req.getResultPrepareListener = reqResultPrepareListenersStacks.getValue
req.regResultPrepareListener = reqResultPrepareListenersStacks.pushValue
req.popResultPrepareListener = reqResultPrepareListenersStacks.popValue

local reqResultPrepareEventListenersStacks = cache.setValueWithHistoryAndCategory()
function reqResultPrepareEventListenersStacks.onGetValue(cate)
    return reqResultPrepareEventListeners[cate]
end
function reqResultPrepareEventListenersStacks.onSetValue(cate, val)
    reqResultPrepareEventListeners[cate] = val
end
req.getResultPrepareEventListener = reqResultPrepareEventListenersStacks.getValue
req.regResultPrepareEventListener = reqResultPrepareEventListenersStacks.pushValue
req.popResultPrepareEventListener = reqResultPrepareEventListenersStacks.popValue

local reqRequestDataPrepareListenersStacks = cache.setValueWithHistoryAndCategory()
function reqRequestDataPrepareListenersStacks.onGetValue(cate)
    return reqRequestDataPrepareListeners[cate]
end
function reqRequestDataPrepareListenersStacks.onSetValue(cate, val)
    reqRequestDataPrepareListeners[cate] = val
end
req.getRequestDataPrepareListener = reqRequestDataPrepareListenersStacks.getValue
req.regRequestDataPrepareListener = reqRequestDataPrepareListenersStacks.pushValue
req.popRequestDataPrepareListener = reqRequestDataPrepareListenersStacks.popValue

local function reqPrepareRequestData(url, data)
    for k, v in spairs(reqRequestDataPrepareListeners) do
        if type(v) == "function" then
            v(url, data)
        end
    end
end

local function reqPrepareResult(request)
    local ret = nil
    for k, v in pairs(reqResultPrepareListeners) do
        if type(v) == "function" then
            ret = v(request)
        end
    end
    if request.event ~= "none" then
        for k, v in pairs(reqResultPrepareEventListeners) do
            if type(v) == "function" then
                ret = v(request)
            end
        end
    end
    return ret
end

local function reqHandleEventData(event)
    if type(event) == "table" then
        local ret = nil
        for k, v in pairs(event) do
            if type(reqEventListeners[k]) == "function" then
                ret = reqEventListeners[k](v)
            end
        end
        return ret
    end
end

function req.defaultOnFailed(request)
    local failedWait = { }
    if request.failed == "network" or request.failed == "timedout" then
        failedWait.done = true
        failedWait.retry = true
    else
        failedWait.done = true
    end
    return failedWait
end

function req.post(uri, data, oncomplete, onfailed, quiet, timeout)
    local ret
    local postdone = nil

    local function realOnComplete(request)
        local prepareWait = reqPrepareResult(request)
        if prepareWait then
            while not prepareWait.done do
                coroutine.yield()
            end
            unity.waitForEndOfFrame()
        end

        local defaultListener = reqDefaultListeners[uri]
        if type(defaultListener) == "function" then
            xpcall(function() defaultListener(request) end, function(err) dump(err) end)
        end

        local eventWait = reqHandleEventData(request.event)
        if eventWait then
            while not eventWait.done do
                coroutine.yield()
            end
            unity.waitForEndOfFrame()
        end

        postdone = "complete"
    end

    local function realOnFailed(request)
        local prepareWait = reqPrepareResult(request)
        if prepareWait then
            while not prepareWait.done do
                coroutine.yield()
            end
            unity.waitForEndOfFrame()
        end

        if request.event ~= "none" then
            local eventWait = reqHandleEventData(request.event)
            if eventWait then
                while not eventWait.done do
                    coroutine.yield()
                end
                unity.waitForEndOfFrame()
            end
        end

        if type(onfailed) == "function" then
            postdone = "failed"
        elseif type(onfailed) == "table" and onfailed[request.failed] then
            postdone = "failed"
        else
            if quiet then
                if quiet == "retry" then
                    ret = ret:repost()
                else
                    postdone = "failed"
                end
            else
                local failedWait = nil
                if type(reqDefaultErrorListeners[request.failed]) == "function" then
                    failedWait = reqDefaultErrorListeners[request.failed](request)
                else
                    failedWait = req.defaultOnFailed(request)
                end

                if failedWait then
                    while not failedWait.done do
                        coroutine.yield()
                    end
                    unity.waitForEndOfFrame()
        
                    if failedWait.retry then
                        ret = ret:repost()
                    else
                        postdone = "failed"
                    end
                else
                    postdone = "failed"
                end
            end
        end
    end

    local realdata = {
        data = data,
        headers = {},
    }
    reqPrepareRequestData(uri, realdata)
    ret = api.post(uri, realdata, quiet, timeout)
    ret.doneFuncs = {
        onFailed = realOnFailed,
        onComplete = realOnComplete,
    }

    local alldone = nil
    while not alldone do
        while not postdone do
            coroutine.yield()
        end
        unity.waitForEndOfFrame()

        if postdone == "complete" then
            if type(oncomplete) == "function" then
                oncomplete(ret)
            end
            alldone = true
        elseif postdone == "failed" then
            local failedWait = nil
            if type(onfailed) == "function" then
                failedWait = onfailed(ret)
            elseif type(onfailed) == "table" and onfailed[ret.failed] then
                if type(onfailed[ret.failed]) == "function" then
                    failedWait = onfailed[ret.failed](ret)
                end
            end
            if failedWait then
                while not failedWait.done do
                    coroutine.yield()
                end
                unity.waitForEndOfFrame()
    
                if failedWait.retry then
                    postdone = nil
                    ret = ret:repost()
                else
                    alldone = true
                end
            else
                alldone = true
            end
        end
    end

    return ret
end

return req