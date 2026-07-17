namespace VocaChat.WebApi.Dtos.Relationships;

/// <summary>表示客户端修改好友关系数值时提交的数据。</summary>
public sealed class UpdateAiRelationshipRequest
{
    public int Familiarity { get; set; }
    public int Affinity { get; set; }
    public int Trust { get; set; }
}
