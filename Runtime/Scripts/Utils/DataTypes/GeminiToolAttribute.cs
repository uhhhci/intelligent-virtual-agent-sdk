using System;

namespace IVH.Core.IntelligentVirtualAgent.Tools
{
    /// <summary>
    /// Markiert eine public Instanz-Methode als Gemini-Tool. Der GeminiToolManager findet alle so
    /// markierten Methoden auf den konfigurierten toolProviders und generiert das Parameter-Schema
    /// automatisch aus der Methodensignatur. Dadurch stimmen die Schema-Property-Namen immer mit den
    /// C#-Parameternamen überein (der GeminiToolManager bindet Argumente über den Parameternamen).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class GeminiToolAttribute : Attribute
    {
        /// <summary>Name, unter dem das Tool bei Gemini registriert wird (z.B. "set_map_zoom").</summary>
        public string Name { get; }

        /// <summary>Beschreibung, die dem Modell erklärt, wann/wie das Tool zu nutzen ist.</summary>
        public string Description { get; }

        public GeminiToolAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// Optionale Beschreibung für einen einzelnen Tool-Parameter. Wird in die "description" der
    /// generierten JSON-Schema-Property übernommen. Ohne dieses Attribut wird nur Typ und
    /// Parametername verwendet.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class GeminiToolParamAttribute : Attribute
    {
        public string Description { get; }

        public GeminiToolParamAttribute(string description)
        {
            Description = description;
        }
    }
}
