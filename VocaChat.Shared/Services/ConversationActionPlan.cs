namespace VocaChat.Services;

/// <summary>
/// 表示一条 AI 消息在当前上下文中准备完成的主要交流动作。
/// </summary>
public enum ConversationAction
{
    Acknowledge,
    Answer,
    Ask,
    Share,
    React,
    Comfort,
    Tease,
    Disagree,
    Evade,
    ShiftTopic,
    Close
}

public enum ConversationMessageLength
{
    VeryShort,
    Short,
    Moderate
}

public enum ConversationDirectness
{
    Direct,
    Partial,
    Indirect
}

public enum ConversationQuestionMode
{
    None,
    Optional,
    Natural
}

public enum ConversationEmotionVisibility
{
    Restrained,
    Natural,
    Open
}

public enum ConversationTopicMovement
{
    Stay,
    SlightDrift,
    Shift
}

public enum ConversationPunctuationRhythm
{
    Sparse,
    Natural,
    Expressive
}

public enum ConversationRelationshipTone
{
    Unknown,
    Distant,
    Reserved,
    Familiar,
    Close
}

public enum ConversationRelationshipBalance
{
    Unknown,
    Balanced,
    SpeakerMoreInvested,
    OtherMoreInvested
}

/// <summary>
/// 保存一次模型生成前已经确定的行为意图和表达边界。
/// </summary>
public sealed record ConversationActionPlan(
    ConversationAction Action,
    ConversationMessageLength MessageLength,
    ConversationDirectness Directness,
    ConversationQuestionMode QuestionMode,
    ConversationEmotionVisibility EmotionVisibility,
    ConversationTopicMovement TopicMovement,
    ConversationPunctuationRhythm PunctuationRhythm,
    ConversationRelationshipTone RelationshipTone,
    ConversationRelationshipBalance RelationshipBalance,
    bool MayOmitObviousContext,
    bool MayLeaveThoughtOpen);
