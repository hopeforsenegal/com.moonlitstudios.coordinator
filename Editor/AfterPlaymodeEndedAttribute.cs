[System.AttributeUsage(System.AttributeTargets.Method)]
public class AfterPlaymodeEndedAttribute : System.Attribute
{
    // This is an attribute (and not a Action) because we want to call a build script after our tests (and building can't be done in playmode). 
    // We didn't have a working example that actually needed parameters but if you have a working example that does need it, we should be able to add it.
}