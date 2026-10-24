namespace FleetPulse.Application;

public sealed record AgentReply(
    string Mode,
    bool UsesLanguageModel,
    string Answer,
    object Evidence);
