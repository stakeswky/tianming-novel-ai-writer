namespace TM.Services.Framework.AI.Core.Routing;

public interface IAIModelRouter
{
    UserConfiguration Resolve(AITaskPurpose purpose);
}
