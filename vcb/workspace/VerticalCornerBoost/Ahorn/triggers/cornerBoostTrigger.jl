module VCB_CornerBoostTrigger

using ..Ahorn, Maple

# width / height 是放置时的默认值；传 "rectangle" 给 EntityPlacement，
# Ahorn 就会把它当成可拖动缩放的矩形触发器。
# enabled / requireGrab 用 String 接收（leave / enable / disable），
# C# 侧走 Enum.TryParse(ignoreCase: true)，不在乎大小写。
@mapdef Trigger "VerticalCornerBoost/CornerBoostTrigger" CornerBoostTrigger(
    x::Integer, y::Integer,
    width::Integer=16, height::Integer=16,
    enabled::String="leave",
    requireGrab::String="leave",
    speed::Number=0.0,
    revertOnLeave::Bool=true,
    revertOnDeath::Bool=true,
    coversScreen::Bool=false,
    flag::String="",
    flagInverted::Bool=false)

const placements = Ahorn.PlacementDict(
    "Vertical Corner Boost Trigger (Vertical Corner Boost)" => Ahorn.EntityPlacement(
        CornerBoostTrigger,
        "rectangle"
    )
)

end
