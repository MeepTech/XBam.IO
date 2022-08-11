using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Meep.Tech.XBam.IO.Configuration {

  /// <summary>
  /// A special contract resolver used for the model serialization and deserialization
  /// </summary>
  public class ModelPortingSaveJsonContract : Model.Serializer.DefaultContractResolver {

    /// <summary>
    /// A special contract resolver used for the model serialization and deserialization
    /// </summary>
    internal ModelPortingSaveJsonContract(Universe universe) 
      : base(universe) {}

    ///<summary><inheritdoc/></summary>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
      var props = base.CreateProperties(type, memberSerialization);
      foreach (var prop in props) {
        var member = type.GetMember(prop.UnderlyingName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Single();
        if (prop.Ignored) {
          if (member.IsDefined(typeof(SaveDataAttribute), true)) {
            prop.Ignored = false;
          }
        } else {
          if (member.IsDefined(typeof(NotSaveDataAttribute), true)) {
            prop.Ignored = true;
          }
        }
      }

      return props;
    }
  }
}
