using System;

namespace Meep.Tech.XBam.IO {

  /// <summary>
  /// Indicates that when this field is serialized or deserialized it should try to load the models as a collection; first via the cache, then via porting if a string id was provided instead of a json object.
  /// This also applies Caching to auto-building.
  /// </summary>
  [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
  public class AutoPortAttribute : Attribute {

    /// <summary>
    /// If true, and the value is a dictionary[string,Iunique]; 
    /// this will save the value as an object, and set the keys for each id in the collection to whatever they are in the existing dictionary.
    /// <para>
    /// By default, this is false, and dictionaries will serialize to a collection of ids, and deserialize to a collection of IUnique models indexed by their id.
    /// </para>
    /// </summary>
    public bool PreserveKeys {
      get;
      init;
    } = false;

    /// <summary>
    /// If this is true, this will not try to auto port when auto-building.
    /// This is false by default.
    /// </summary>
    public bool IgnoreDuringAutoBuilding {
      get;
      init;
    } = false;
  }
}
