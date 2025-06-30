namespace ErrorProne.NET.Core.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public class UseConfigureAwaitFalseAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MustUseResultAttribute : System.Attribute { }
}
