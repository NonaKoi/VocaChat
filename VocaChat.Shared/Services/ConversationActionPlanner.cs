namespace VocaChat.Services;

/// <summary>
/// 根据场景、人物倾向、当前内容和关系状态制定单次消息生成计划。
/// </summary>
public sealed class ConversationActionPlanner
{
    private readonly Random _random;

    public ConversationActionPlanner()
        : this(Random.Shared)
    {
    }

    internal ConversationActionPlanner(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// 为生成请求补充短生命周期计划；不会改变发言者、消息数量或轮次规则。
    /// </summary>
    public AiMessageGenerationRequest ApplyPlan(
        AiMessageGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request with { ActionPlan = CreatePlan(request) };
    }

    internal ConversationActionPlan CreatePlan(
        AiMessageGenerationRequest request)
    {
        ConversationRelationshipTone relationshipTone = GetRelationshipTone(
            request.SpeakerToOtherRelationshipScore);
        ConversationRelationshipBalance relationshipBalance =
            GetRelationshipBalance(
                request.SpeakerToOtherRelationshipScore,
                request.OtherToSpeakerRelationshipScore);
        ConversationAction action = ChooseAction(
            request,
            relationshipTone);

        return CreateExpressionPlan(
            request,
            action,
            relationshipTone,
            relationshipBalance);
    }

    /// <summary>
    /// 为导演已经选定的合法交流动作补充长度、直接程度和语气等表达边界。
    /// </summary>
    internal ConversationActionPlan CreatePlan(
        AiMessageGenerationRequest request,
        ConversationAction action)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreateExpressionPlan(
            request,
            action,
            GetRelationshipTone(request.SpeakerToOtherRelationshipScore),
            GetRelationshipBalance(
                request.SpeakerToOtherRelationshipScore,
                request.OtherToSpeakerRelationshipScore));
    }

    private ConversationAction ChooseAction(
        AiMessageGenerationRequest request,
        ConversationRelationshipTone relationshipTone)
    {
        if (request.Scenario ==
            AiMessageGenerationScenario.AutonomousPrivateChatClosing)
        {
            return ConversationAction.Close;
        }

        string targetContent = request.ReplyTarget?.Message?.Content
            ?? request.FocusContent;
        if (request.ReplyTarget?.Kind == AiDialogueReplyTargetKind.Message
            && (LooksLikeDirectQuestion(targetContent)
                || LooksLikeDirectRequest(targetContent)))
        {
            return ConversationAction.Answer;
        }

        Dictionary<ConversationAction, double> weights =
            CreateBaseWeights(request);
        ApplyContentWeights(weights, targetContent);
        ApplyProfileWeights(
            weights,
            $"{request.Speaker.Personality} {request.Speaker.SpeakingStyle}");
        ApplyRelationshipWeights(weights, relationshipTone);

        return ChooseWeightedAction(weights);
    }

    private static Dictionary<ConversationAction, double> CreateBaseWeights(
        AiMessageGenerationRequest request)
    {
        return request.Scenario switch
        {
            AiMessageGenerationScenario.UserPrivateChat => new()
            {
                [ConversationAction.Acknowledge] = 18,
                [ConversationAction.Ask] = 12,
                [ConversationAction.Share] = 24,
                [ConversationAction.React] = 22,
                [ConversationAction.Comfort] = 6,
                [ConversationAction.Tease] = 6,
                [ConversationAction.Disagree] = 5,
                [ConversationAction.Evade] = 4,
                [ConversationAction.ShiftTopic] = 3
            },
            AiMessageGenerationScenario.GroupPrimaryReply => new()
            {
                [ConversationAction.Acknowledge] = 15,
                [ConversationAction.Ask] = 10,
                [ConversationAction.Share] = 28,
                [ConversationAction.React] = 22,
                [ConversationAction.Comfort] = 5,
                [ConversationAction.Tease] = 7,
                [ConversationAction.Disagree] = 8,
                [ConversationAction.Evade] = 2,
                [ConversationAction.ShiftTopic] = 3
            },
            AiMessageGenerationScenario.GroupFollowUpReply => new()
            {
                [ConversationAction.Acknowledge] = 8,
                [ConversationAction.Ask] = 7,
                [ConversationAction.Share] = 21,
                [ConversationAction.React] = 31,
                [ConversationAction.Comfort] = 5,
                [ConversationAction.Tease] = 10,
                [ConversationAction.Disagree] = 13,
                [ConversationAction.Evade] = 2,
                [ConversationAction.ShiftTopic] = 3
            },
            AiMessageGenerationScenario.AutonomousPrivateChat when
                request.IsInitiator => new()
            {
                [ConversationAction.Acknowledge] = 5,
                [ConversationAction.Ask] = 25,
                [ConversationAction.Share] = 34,
                [ConversationAction.React] = 5,
                [ConversationAction.Comfort] = 4,
                [ConversationAction.Tease] = 10,
                [ConversationAction.Disagree] = 3,
                [ConversationAction.Evade] = 3,
                [ConversationAction.ShiftTopic] = 11
            },
            AiMessageGenerationScenario.AutonomousPrivateChat => new()
            {
                [ConversationAction.Acknowledge] = 17,
                [ConversationAction.Ask] = 13,
                [ConversationAction.Share] = 20,
                [ConversationAction.React] = 24,
                [ConversationAction.Comfort] = 7,
                [ConversationAction.Tease] = 7,
                [ConversationAction.Disagree] = 5,
                [ConversationAction.Evade] = 4,
                [ConversationAction.ShiftTopic] = 3
            },
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scenario))
        };
    }

    private static void ApplyContentWeights(
        IDictionary<ConversationAction, double> weights,
        string content)
    {
        if (ContainsAny(
                content,
                "难过", "伤心", "烦", "累", "委屈", "害怕", "失落", "崩溃"))
        {
            AddWeight(weights, ConversationAction.Comfort, 35);
            AddWeight(weights, ConversationAction.Tease, -4);
            AddWeight(weights, ConversationAction.ShiftTopic, -2);
        }

        if (content.Contains('?')
            || content.Contains('？')
            || ContainsAny(content, "怎么", "为什么", "什么", "哪", "吗", "呢"))
        {
            AddWeight(weights, ConversationAction.Share, 12);
            AddWeight(weights, ConversationAction.Acknowledge, 6);
            AddWeight(weights, ConversationAction.Evade, 4);
        }

        if (ContainsAny(content, "哈哈", "笑死", "好玩", "离谱", "绝了"))
        {
            AddWeight(weights, ConversationAction.React, 14);
            AddWeight(weights, ConversationAction.Tease, 12);
        }
    }

    private static void ApplyProfileWeights(
        IDictionary<ConversationAction, double> weights,
        string profile)
    {
        if (ContainsAny(profile, "温柔", "体贴", "善良", "耐心"))
        {
            AddWeight(weights, ConversationAction.Comfort, 10);
            AddWeight(weights, ConversationAction.Acknowledge, 6);
        }

        if (ContainsAny(profile, "幽默", "活泼", "调皮", "毒舌"))
        {
            AddWeight(weights, ConversationAction.Tease, 14);
            AddWeight(weights, ConversationAction.React, 7);
        }

        if (ContainsAny(profile, "安静", "内向", "寡言", "冷淡"))
        {
            AddWeight(weights, ConversationAction.React, 8);
            AddWeight(weights, ConversationAction.Evade, 8);
            AddWeight(weights, ConversationAction.Ask, -4);
        }

        if (ContainsAny(profile, "直接", "坦率", "理性", "认真"))
        {
            AddWeight(weights, ConversationAction.Share, 8);
            AddWeight(weights, ConversationAction.Disagree, 7);
        }
    }

    private static void ApplyRelationshipWeights(
        IDictionary<ConversationAction, double> weights,
        ConversationRelationshipTone relationshipTone)
    {
        switch (relationshipTone)
        {
            case ConversationRelationshipTone.Distant:
                AddWeight(weights, ConversationAction.Acknowledge, 10);
                AddWeight(weights, ConversationAction.Evade, 10);
                AddWeight(weights, ConversationAction.Tease, -5);
                break;
            case ConversationRelationshipTone.Reserved:
                AddWeight(weights, ConversationAction.Acknowledge, 6);
                AddWeight(weights, ConversationAction.React, 4);
                break;
            case ConversationRelationshipTone.Familiar:
                AddWeight(weights, ConversationAction.Share, 7);
                AddWeight(weights, ConversationAction.Ask, 5);
                AddWeight(weights, ConversationAction.Tease, 4);
                break;
            case ConversationRelationshipTone.Close:
                AddWeight(weights, ConversationAction.Share, 10);
                AddWeight(weights, ConversationAction.Tease, 9);
                AddWeight(weights, ConversationAction.Comfort, 7);
                AddWeight(weights, ConversationAction.Evade, -3);
                break;
        }
    }

    private ConversationAction ChooseWeightedAction(
        IReadOnlyDictionary<ConversationAction, double> weights)
    {
        double total = weights.Values.Sum(value => Math.Max(0, value));
        double roll = _random.NextDouble() * total;

        foreach ((ConversationAction action, double weight) in weights)
        {
            roll -= Math.Max(0, weight);
            if (roll < 0)
            {
                return action;
            }
        }

        return ConversationAction.React;
    }

    private ConversationActionPlan CreateExpressionPlan(
        AiMessageGenerationRequest request,
        ConversationAction action,
        ConversationRelationshipTone relationshipTone,
        ConversationRelationshipBalance relationshipBalance)
    {
        ConversationMessageLength length = action switch
        {
            ConversationAction.React or ConversationAction.Acknowledge
                or ConversationAction.Evade or ConversationAction.Close
                => ConversationMessageLength.VeryShort,
            ConversationAction.Answer or ConversationAction.Share
                or ConversationAction.Comfort
                or ConversationAction.Disagree
                => ConversationMessageLength.Short,
            _ => ConversationMessageLength.Short
        };
        if (request.Scenario == AiMessageGenerationScenario.UserPrivateChat
            && action == ConversationAction.Answer
            && RequiresExpandedAnswer(request))
        {
            length = ConversationMessageLength.Moderate;
        }
        ConversationDirectness directness = action switch
        {
            ConversationAction.Evade or ConversationAction.ShiftTopic
                => ConversationDirectness.Indirect,
            ConversationAction.React or ConversationAction.Acknowledge
                or ConversationAction.Tease
                => ConversationDirectness.Partial,
            _ => ConversationDirectness.Direct
        };
        ConversationQuestionMode questionMode = action switch
        {
            ConversationAction.Ask => ConversationQuestionMode.Natural,
            ConversationAction.Share when _random.NextDouble() < 0.25
                => ConversationQuestionMode.Optional,
            _ => ConversationQuestionMode.None
        };
        ConversationEmotionVisibility emotionVisibility = action switch
        {
            ConversationAction.React or ConversationAction.Comfort
                or ConversationAction.Tease
                => ConversationEmotionVisibility.Open,
            ConversationAction.Evade or ConversationAction.Disagree
                => ConversationEmotionVisibility.Restrained,
            _ => ConversationEmotionVisibility.Natural
        };
        ConversationTopicMovement topicMovement = action switch
        {
            ConversationAction.ShiftTopic => ConversationTopicMovement.Shift,
            ConversationAction.Share or ConversationAction.Tease
                => ConversationTopicMovement.SlightDrift,
            _ => ConversationTopicMovement.Stay
        };
        ConversationPunctuationRhythm punctuationRhythm =
            emotionVisibility == ConversationEmotionVisibility.Open
                ? ConversationPunctuationRhythm.Expressive
                : ConversationPunctuationRhythm.Natural;

        string profile =
            $"{request.Speaker.Personality} {request.Speaker.SpeakingStyle}";
        if (ContainsAny(profile, "安静", "内向", "寡言", "冷淡"))
        {
            if (action != ConversationAction.Answer)
            {
                length = ConversationMessageLength.VeryShort;
            }

            punctuationRhythm = ConversationPunctuationRhythm.Sparse;
            emotionVisibility = ConversationEmotionVisibility.Restrained;
        }
        else if (ContainsAny(profile, "话多", "健谈", "详细"))
        {
            length = ConversationMessageLength.Moderate;
        }

        if (relationshipTone is ConversationRelationshipTone.Distant
            or ConversationRelationshipTone.Reserved)
        {
            emotionVisibility = ConversationEmotionVisibility.Restrained;
        }
        else if (relationshipTone == ConversationRelationshipTone.Close
                 && action != ConversationAction.Disagree)
        {
            emotionVisibility = ConversationEmotionVisibility.Open;
        }

        return new ConversationActionPlan(
            action,
            length,
            directness,
            questionMode,
            emotionVisibility,
            topicMovement,
            punctuationRhythm,
            relationshipTone,
            relationshipBalance,
            MayOmitObviousContext: action is not ConversationAction.Ask,
            MayLeaveThoughtOpen: action is ConversationAction.React
                or ConversationAction.Acknowledge
                or ConversationAction.Evade
                or ConversationAction.Share);
    }

    private static bool RequiresExpandedAnswer(
        AiMessageGenerationRequest request)
    {
        string content = request.ReplyTarget?.Message?.Content
            ?? request.FocusContent;
        return ContainsAny(
            content,
            "具体", "详细", "展开", "讲讲", "说说", "解释",
            "多说", "几句", "分几条", "为什么", "怎么回事");
    }

    private static bool LooksLikeDirectQuestion(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string normalized = content.Trim();
        if (normalized.Contains('?') || normalized.Contains('？'))
        {
            return true;
        }

        string[] questionOpenings =
        {
            "怎么", "为什么", "什么", "哪", "谁", "多少", "几点", "是否"
        };
        string[] questionEndings =
        {
            "吗", "能不能", "可不可以", "有没有", "要不要", "是不是",
            "怎么样", "干嘛", "什么", "去哪", "多少", "几点"
        };

        return questionOpenings.Any(opening => normalized.StartsWith(
                opening,
                StringComparison.OrdinalIgnoreCase))
            || questionEndings.Any(ending => normalized.EndsWith(
                ending,
                StringComparison.OrdinalIgnoreCase))
            || ContainsAny(
                normalized,
                "能不能", "可不可以", "有没有", "要不要", "是不是",
                "几点", "什么时候", "在哪", "去哪", "谁来", "谁会", "多少");
    }

    private static bool LooksLikeDirectRequest(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        string normalized = content.Trim();
        string[] requestOpenings =
        {
            "请", "说说", "说一下", "告诉我", "回答", "解释", "选一个", "帮我"
        };

        return requestOpenings.Any(opening => normalized.StartsWith(
                opening,
                StringComparison.OrdinalIgnoreCase))
            || ContainsAny(
                normalized,
                "请说", "请回答", "请告诉", "请评价", "请选",
                "说一种", "说一个", "说说", "讲讲", "展开讲",
                "评价一下", "回答一下");
    }

    private static ConversationRelationshipTone GetRelationshipTone(
        double? score)
    {
        return score switch
        {
            null => ConversationRelationshipTone.Unknown,
            < 25 => ConversationRelationshipTone.Distant,
            < 45 => ConversationRelationshipTone.Reserved,
            < 70 => ConversationRelationshipTone.Familiar,
            _ => ConversationRelationshipTone.Close
        };
    }

    private static ConversationRelationshipBalance GetRelationshipBalance(
        double? speakerToOtherScore,
        double? otherToSpeakerScore)
    {
        if (speakerToOtherScore is null || otherToSpeakerScore is null)
        {
            return ConversationRelationshipBalance.Unknown;
        }

        double difference = speakerToOtherScore.Value - otherToSpeakerScore.Value;
        return difference switch
        {
            > 15 => ConversationRelationshipBalance.SpeakerMoreInvested,
            < -15 => ConversationRelationshipBalance.OtherMoreInvested,
            _ => ConversationRelationshipBalance.Balanced
        };
    }

    private static void AddWeight(
        IDictionary<ConversationAction, double> weights,
        ConversationAction action,
        double amount)
    {
        double currentWeight = weights.TryGetValue(action, out double value)
            ? value
            : 0;
        weights[action] = Math.Max(0, currentWeight + amount);
    }

    private static bool ContainsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate =>
            value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }
}
