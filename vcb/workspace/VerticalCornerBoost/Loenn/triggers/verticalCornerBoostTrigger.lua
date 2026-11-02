local verticalCornerBoostTrigger = {}

verticalCornerBoostTrigger.name = "VerticalCornerBoost/VerticalCornerBoostTrigger"
verticalCornerBoostTrigger.placements = {
    {
        name = "trigger",
        data = {
            -- width / height 只是**放置时的默认值**：Loenn 的 entities.canResize 判定的是
            -- “实体有没有 width/height 字段”，只要字段存在就能在房间里拖动缩放。
            -- 默认给整格大小，省得每次都要手动拉。
            width = 16,
            height = 16,
            enabled = "leave",
            requireGrab = "leave",
            speed = 0.0,
            revertOnLeave = true,
            revertOnDeath = true,
            coversScreen = false,
            flag = "",
            flagInverted = false
        }
    }
}

verticalCornerBoostTrigger.fieldInformation = {
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

return verticalCornerBoostTrigger