
namespace FiftyOne.Pipeline.Translation.Data;

public enum MissingTranslationBehaviour
{
    /// <summary>
    /// Use the original value if there is no translation for it.
    /// This is the default behaviour.
    /// </summary>
    Original,

    /// <summary>
    /// Use an empty string if there is no translation for the value.
    /// </summary>
    EmptyString,

    /// <summary>
    /// Add a flow error if there is no translation for the value. The error
    /// contains the reason there was no translation.
    /// </summary>
    FlowError
}
