using System;

namespace Meep.Tech.XBam.IO {

  /// <summary>
  /// Indicates this field is NOT save data, and even if it's labeled as json include or column, it should be ignored in server side serialization data
  /// </summary>

  [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
  public class NotSaveDataAttribute : Attribute {}
}
