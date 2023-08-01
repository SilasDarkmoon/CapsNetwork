volatilecache = {}

local rawCache = {}

function volatilecache.clear()
    for k, v in pairs(rawCache) do
        rawCache[k] = nil
    end
end

function volatilecache.shouldclear(resp)
    if api.success(resp) then
        if type(resp.val) == "table" and resp.val ~= clr.null then
            if next(resp.val) then
                return true
            end
        end
    end
end

function volatilecache.onresp(resp)
    if volatilecache.shouldclear(resp) then
        volatilecache.clear()
    end
end

reqDefaultListeners[""] = combinefunc(reqDefaultListeners[""], function(resp)
    volatilecache.onresp(resp)
end)

local function vcIndex(tab, key)
    local subcache = rawCache[key]
    if not subcache then
        subcache = {}
        rawCache[key] = subcache
    end
    return subcache
end

local function vcNewIndex(tab, key, val)
    rawCache[key] = val
end

setmetatable(volatilecache, {
    __index = vcIndex,
    __newindex = vcNewIndex,
})


return volatilecache