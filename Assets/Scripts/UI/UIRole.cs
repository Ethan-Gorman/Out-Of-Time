using UnityEngine;
using UnityEngine.UI;

// Opt-in theming for new UI: add it to a panel Image or a Text and pick what it is.
// It restyles itself whenever the era's UITheme changes, so it also works on objects that start inactive
public class UIRole : MonoBehaviour
{
    public enum Kind { Panel, Banner, Text, TextDim, Heading }

    public Kind kind;

    void OnEnable()
    {
        UITheme.Changed += Apply;
        Apply(UITheme.Current);
    }

    void OnDisable() => UITheme.Changed -= Apply;

    void Apply(UITheme t)
    {
        switch (kind)
        {
            case Kind.Panel: UIThemer.StylePanel(gameObject, UISprites.Panel(t)); break;
            case Kind.Banner: UIThemer.StylePanel(gameObject, UISprites.Banner(t)); break;
            case Kind.Text: UIThemer.StyleText(GetComponent<Text>(), t.text); break;
            case Kind.TextDim: UIThemer.StyleText(GetComponent<Text>(), t.textDim); break;
            case Kind.Heading: UIThemer.StyleText(GetComponent<Text>(), t.accent); break;
        }
    }
}
