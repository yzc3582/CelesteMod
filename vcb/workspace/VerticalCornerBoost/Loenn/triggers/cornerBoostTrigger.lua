local cornerBoostTrigger = {}

cornerBoostTrigger.name = "VerticalCornerBoost/CornerBoostTrigger"
cornerBoostTrigger.placements = {
    name = "trigger",
    data = {
        width = 8,
        height = 8,
        enabled = "leave",
        requireGrab = "leave",
        speed = 0.0,
        revertOnLeave = true,
        revertOnDeath = true,
        coversScreen = false
    }
}

cornerBoostTrigger.fieldInformation = {
    enabled = {
        options = {
            { "Leave unchanged", "leave" },
            { "Enable", "enable" },
            { "Disable", "disable" }
        },
        editable = false
    },
    requireGrab = {
        options = {
            { "Leave unchanged", "leave" },
            { "Require grabbing", "enable" },
            { "Do not require grabbing", "disable" }
        },
        editable = false
    },
    speed = {
        fieldType = "number",
        minimumValue = 0.0
    }
}

return cornerBoostTrigger
