using System;

namespace Meep.Tech.XBam.IO {
  
  /// <summary>
  /// Indicates this field is save data, and even if it's labeled as json ignore, it should be included in server side serialization data
  /// </summary>

  [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
  public class SaveDataAttribute : Attribute {

    /// <summary>
    /// A property value getter
    /// </summary>
    public delegate object Getter(object fromModel);

    /// <summary>
    /// A property value setter
    /// </summary>
    public delegate void Setter(object toModel, object value);

    /// <summary>
    /// Override for the json property name
    /// </summary>
    public virtual string PropertyNameOverride { get; init; }

    /// <summary>
    /// Get the override for the getter.
    /// </summary>
    public virtual Getter GetGetterOverride(System.Reflection.PropertyInfo property, Universe universe)
      => null;

    /// <summary>
    /// Get the override for the setter
    /// </summary>
    public virtual Setter GetSetterOverride(System.Reflection.PropertyInfo property, Universe universe)
      => null;

    /// <summary>
    /// Used to deserialize the data from raw data
    /// </summary>
    public virtual Func<object, object> DeserializerFromRawOverride(Universe universe)
      => null;

    /// <summary>
    /// Used to serialize the data into raw data
    /// </summary>
    public virtual Func<object, object> SerializerToRawOverride(Universe universe)
      => null;
  }
}
