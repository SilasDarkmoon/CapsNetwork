req = { }
url = { }

reqDefaultListeners = { }
reqDefaultErrorListeners = { }
reqEventListeners = { }
reqResultPrepareListeners = { }
reqResultPrepareEventListeners = { }

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
    local resDlg, dlgccomp
    local failedWait = { }
    if request.failed == "network" or request.failed == "timedout" then
        --resDlg = require("ui.control.manager.DialogManager").ShowRetryPop(lang.trans("tips"), request.msg, function()
            failedWait.done = true
            failedWait.retry = true
        --end)
    else
        --resDlg = require("ui.control.manager.DialogManager").ShowAlertPop(lang.trans("tips"), request.msg, function()
            failedWait.done = true
        --end)
    end

    -- local canvas = resDlg:GetComponent(typeof(CS.UnityEngine.Canvas))
    -- canvas.sortingOrder = 20010
    return failedWait
end

function req.post(uri, data, oncomplete, onfailed, quiet, timeOut)
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
                postdone = "failed"
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

    ret = api.post(uri, data, quiet, timeOut)
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


-- ------------
-- -- default eventListener:
-- ------------

-- function reqEventListeners.restart(data)
--     local waitHandle = { }
--     if data then
--         local msg  = type(data) == "string" and data or lang.trans("loginExpire")
--         require("ui.control.manager.DialogManager").ShowAlertPop(lang.trans("tips"), msg, function()
--             waitHandle.done = true
--             unity.restart()
--         end, nil, nil, 10000)
--     end
--     return waitHandle
-- end

-- function reqEventListeners.authExpire(data)
--     local waitHandle = { }
--     if data then
--         local msg  = type(data) == "string" and data or lang.trans("loginExpire")
--         require("ui.control.manager.DialogManager").ShowAlertPop(lang.trans("tips"), msg, function()
--             waitHandle.done = true
--             luaevt.trig("player_authExpire")
--         end, nil, nil, 10000)
--     end
--     return waitHandle
-- end

-- ------------
-- -- categoried error listeners:
-- ------------
-- reqDefaultErrorListeners[1002] = function(request)
--     if request and type(request.val) == "userdata" then
--         unity.changeServerTo(CS.toluastring(request.val))
--     end
-- end
-- ------------
-- -- requests:
-- ------------
-- local function SetCheckVersionVer(data)
--     local gameVer = _G["___resver"]
--     local isEditor = CS.UnityEngine.Application.isEditor
--     if CS.SplitResManager ~= nil and CS.SplitResManager.IsSplitResApp == true then
--         if isEditor then data.plat = "Android" end
--         local ver = {}
--         if type(gameVer) == "table" then
--             for k,v in pairs(gameVer) do
--                 if k ~= "res" and k ~= "resex" and k ~= "editor" then ver[k] = v end
--             end
--             local cdnv = tonumber(gameVer["cdn"])
--             for i = 1 , 9 ,1 do ver["cdn" .. i] = cdnv end
--         end
--         gameVer = ver
--         if isEditor then dump(gameVer, "测试模式运行 [Android]：   ") end
--     end
--     data.ver = gameVer
--     return data
-- end

-- function req.checkVersion(oncomplete, onfailed)
--     local flags = {}
--     local udFlags = CS.table(CS.Capstones.UnityFramework.ResManager.GetDistributeFlags())
--     for i, v in ipairs(udFlags) do
--         table.insert(flags, CS.toluastring(v))
--     end
--     local lang, country = luaevt.trig("GetLang")

--     local data = {
--         capid = CS.capid(),
--         plat = CS.platform,
--         flags = flags,
--         udid = luaevt.trig("GetUDID") or luaevt.trig("SDK_AdvertisingId") or CS.capid(),
--         mac = luaevt.trig("GetMacAddr"),
--         imei = luaevt.trig("GetImei"),
--         bichannel = luaevt.trig("SDK_GetChannel"),
--         app = luaevt.trig("SDK_GetAppId"),
--         appver = luaevt.trig("SDK_GetAppVerCode"),
--         pf = CS.platform,
--         lang = lang,
--         country = country,
--     }
--     SetCheckVersionVer(data)
--     if luaevt.trig("Platform_Branch") == nil then
--         data.fdataChannel = CS.Msdk.WGPlatform.Instance:WGGetChannelId()
--         data.regChannelId = CS.Msdk.WGPlatform.Instance:WGGetRegisterChannelId()
--     else
--         data.fdataChannel = luaevt.trig("SDK_GetChannel")
--         data.regChannelId = luaevt.trig("SDK_GetChannel")
--     end
--     return req.post(tostring(___CONFIG__ACCOUNT_URL) .. "device/version", data, oncomplete, onfailed)
-- end

-- function req.checkCheat(oncomplete, onfailed)
--     local flags = {}
--     local udFlags = CS.table(CS.Capstones.UnityFramework.ResManager.GetDistributeFlags())
--     for i, v in ipairs(udFlags) do
--         table.insert(flags, CS.toluastring(v))
--     end
--     local data = {
--         capid = CS.capid(),
--         plat = CS.platform,
--         flags = flags,
--         udid = luaevt.trig("GetUDID") or CS.capid(),
--         mac = luaevt.trig("GetMacAddr"),
--         imei = luaevt.trig("GetImei"),
--         bichannel = luaevt.trig("SDK_GetChannel"),
--     }
--     SetCheckVersionVer(data)
--     return req.post(tostring(___CONFIG__ACCOUNT_URL) .. "device/cheat", data, oncomplete, onfailed, true)
-- end

-- function req.forceColdUpdateInfo()
--     local data = {
--         did = CS.Msdk.WGPlatform.Instance:WGGetChannelId(),
--         plat = CS.platform,
--     }

--     return req.post(tostring(___CONFIG__ACCOUNT_URL) .. "device/forceColdUpdateInfo", data, nil, nil, true)
-- end

-- exports.url = url
-- exports.reqDefaultListeners = reqDefaultListeners
-- exports.reqDefaultErrorListeners = reqDefaultErrorListeners
-- exports.reqEventListeners = reqEventListeners
-- exports.reqResultPrepareListeners = reqResultPrepareListeners
-- exports.reqResultPrepareEventListeners = reqResultPrepareEventListeners

return req