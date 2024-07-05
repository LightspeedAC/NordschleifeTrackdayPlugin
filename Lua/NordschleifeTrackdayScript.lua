local player = ac.getCarState(1)
local collisionTimeout = 0
local cutTimeout = 0
local minCollisionHeight = 0.27
local toTeleportMaxSpeedThreshold = 500
local isCarLockedForPlayer = false
local countCollisionsInPits = true

local pitsCornerA = { x = 549.23, z = 1502.58 }
local pitsCornerB = { x = 696.02, z = 1346.43 }
local pitsMinX, pitsMaxX = math.min(pitsCornerA.x, pitsCornerB.x), math.max(pitsCornerA.x, pitsCornerB.x)
local pitsMinZ, pitsMaxZ = math.min(pitsCornerA.z, pitsCornerB.z), math.max(pitsCornerA.z, pitsCornerB.z)

local function isWithinPits(currentPosition)
    return (currentPosition.x >= pitsMinX and currentPosition.x <= pitsMaxX) and
        (currentPosition.z >= pitsMinZ and currentPosition.z <= pitsMaxZ)
end

local onCarLockedReceive = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsCarLockedReceive"),
    isLocked = ac.StructItem.uint16(),
}, function(sender, message)
    if sender ~= nil then return end

    if message.isLocked == 1 then
        isCarLockedForPlayer = true
    else
        isCarLockedForPlayer = false
    end
end)

local onCarPitCollisionsReceive = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsPitCollisionsReceive"),
    isPitCollisionsCounted = ac.StructItem.uint16(),
}, function(sender, message)
    if sender ~= nil then return end

    if message.isPitCollisionsCounted == 1 then
        countCollisionsInPits = true
    else
        countCollisionsInPits = false
    end
end)

local onCollision = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsEnvironmentCollision"),
    Speed = ac.StructItem.int32()
})
local onCut = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsLapCut"),
    Speed = ac.StructItem.int32()
})
local onPitLeave = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsPitLeave"),
    id = ac.StructItem.int32()
})
local onPitReEntry = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsPitReEntry"),
    id = ac.StructItem.int32()
})
local onPitTeleport = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsPitTeleport"),
    id = ac.StructItem.int32()
})
local onLapStart = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsLapStart"),
    id = ac.StructItem.int32()
})
local onConvoyAlmostFinish = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAlmostFinish"),
    id = ac.StructItem.int32()
})
local onConvoyAtNorthTurn = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtNorthTurn"),
    id = ac.StructItem.int32()
})
local onConvoyAtAirfield = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtAirfield"),
    id = ac.StructItem.int32()
})
local onConvoyAtFoxhole = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtFoxhole"),
    id = ac.StructItem.int32()
})
local onConvoyAtKallenForest = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtKallenForest"),
    id = ac.StructItem.int32()
})
local onConvoyAtWaterMill = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtWaterMill"),
    id = ac.StructItem.int32()
})
local onConvoyAtLittleValley = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtLittleValley"),
    id = ac.StructItem.int32()
})
local onConvoyAtFirstCarousel = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtFirstCarousel"),
    id = ac.StructItem.int32()
})
local onConvoyAtBrunnchen = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtBrunnchen"),
    id = ac.StructItem.int32()
})
local onConvoyAtSecondCarousel = ac.OnlineEvent({
    ac.StructItem.key("lightspeedPointsConvoyAtSecondCarousel"),
    id = ac.StructItem.int32()
})

local Checkpoint = class("Checkpoint")
function Checkpoint:initialize(position, forward, radius)
    self.position = position
    self.normal = forward:normalize()
    self.radius = radius
    self.d = self.normal:dot(position)
end

function Checkpoint:side(position)
    return self.normal:dot(position) - self.d > 0
end

function Checkpoint:passed(previous, current)
    local previousSide = self:side(previous)
    local currentSide = self:side(current)
    return current:closerToThan(self.position, self.radius) and previousSide ~= currentSide and previousSide == false
end

local pitExitCheckpoint = Checkpoint(vec3(585.79, 91.35, 1475.79), vec3(0, 0, 1), 12)
local pitReEntryCheckpoint = Checkpoint(vec3(585.79, 91.35, 1475.79), vec3(0, 0, -1), 12)
local lapStartCheckpoint = Checkpoint(vec3(170.46, 105.64, 1737.48), vec3(0, 0, 1), 20)
local convoyNearFinishCheckpoint = Checkpoint(vec3(1986.75, 74.97, 374.93), vec3(1, 0, 0), 20)
local convoyNorthTurnCheckpoint = Checkpoint(vec3(-625.59, 146.59, 2240.01), vec3(-1, 0, 0), 25)      -- 2km - North Turn
local convoyAirfieldCheckpoint = Checkpoint(vec3(-2177.44, 93.59, 1540.91), vec3(0, 0, -1), 20)       -- 4km - Airfield
local convoyFoxholeCheckpoint = Checkpoint(vec3(-2526.74, 37.83, -16.62), vec3(0, 0, -1), 20)         -- 6km - Foxhole
local convoyKallenForestCheckpoint = Checkpoint(vec3(-1589.83, -31.08, -1700.77), vec3(-1, 0, 0), 20) -- 8km - Kallen Forest
local convoyWaterMillCheckpoint = Checkpoint(vec3(-110.40, -124.35, -2249.71), vec3(0, 0, -1), 20)    -- 10km - Water Mill
local convoyLittleValleyCheckpoint = Checkpoint(vec3(1196.56, -44.42, -1644.69), vec3(1, 0, 0), 20)   -- 12km - Little Valley
local convoyFirstCarouselCheckpoint = Checkpoint(vec3(2099.80, 75.73, -1407.64), vec3(0, 0, 1), 20)   -- 14km - First Carousel
local convoyBrunnchenCheckpoint = Checkpoint(vec3(3439.53, 67.56, -1219.93), vec3(1, 0, 0), 20)       -- 16km - Brunnchen a.k.a Youtube Corner
local convoySecondCarouselCheckpoint = Checkpoint(vec3(1721.88, 68.38, 178.33), vec3(0, 0, 1), 20)    -- 19km - Second Carousel

local lastCarPositions = {}

function DoUpdate(dt, currentPosition)
    local carIndex = 0

    if isCarLockedForPlayer then
        physics.setCarNoInput(true)
        physics.setCarFuel(0, 0)
        physics.forceUserBrakesFor(60, 1)
        physics.forceUserClutchFor(60, 1)
        physics.engageGear(0, 1)
        return
    end

    lastCarPositions[carIndex] = lastCarPositions[carIndex] or currentPosition:clone()
    local lastPosition = lastCarPositions[carIndex]
    local speed = ((currentPosition - lastPosition):length() / dt)
    if speed > toTeleportMaxSpeedThreshold and (isWithinPits(currentPosition)) then
        onPitTeleport { id = carIndex }
    end

    local checkpoints = {
        { checkpoint = pitExitCheckpoint,              event = onPitLeave },
        { checkpoint = pitReEntryCheckpoint,           event = onPitReEntry },
        { checkpoint = lapStartCheckpoint,             event = onLapStart },
        { checkpoint = convoyNearFinishCheckpoint,     event = onConvoyAlmostFinish },
        { checkpoint = convoyNorthTurnCheckpoint,      event = onConvoyAtNorthTurn },
        { checkpoint = convoyAirfieldCheckpoint,       event = onConvoyAtAirfield },
        { checkpoint = convoyFoxholeCheckpoint,        event = onConvoyAtFoxhole },
        { checkpoint = convoyKallenForestCheckpoint,   event = onConvoyAtKallenForest },
        { checkpoint = convoyWaterMillCheckpoint,      event = onConvoyAtWaterMill },
        { checkpoint = convoyLittleValleyCheckpoint,   event = onConvoyAtLittleValley },
        { checkpoint = convoyFirstCarouselCheckpoint,  event = onConvoyAtFirstCarousel },
        { checkpoint = convoyBrunnchenCheckpoint,      event = onConvoyAtBrunnchen },
        { checkpoint = convoySecondCarouselCheckpoint, event = onConvoyAtSecondCarousel }
    }
    for _, item in ipairs(checkpoints) do
        if item.checkpoint:passed(lastPosition, currentPosition) then
            item.event { id = carIndex }
        end
    end

    lastCarPositions[carIndex] = currentPosition:clone()
end

function script.update(dt)
    local car = ac.getCar(0)
    local currentPosition = car.position

    if collisionTimeout > 0 then
        collisionTimeout = collisionTimeout - dt
    elseif player.speedKmh > 0 and player.collisionPosition.y > minCollisionHeight and player.collidedWith >= 0 then
        local inPits = isWithinPits(currentPosition)
        if not inPits or (inPits and countCollisionsInPits) then
            onCollision { Speed = player.speedKmh }
            collisionTimeout = 1
        end
    end

    if cutTimeout > 0 then
        cutTimeout = cutTimeout - dt
    elseif player.speedKmh > 1 and player.wheelsOutside > 3 then
        onCut { Speed = player.speedKmh }
        cutTimeout = 1
    end

    DoUpdate(dt, currentPosition)
end
