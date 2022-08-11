using Meep.Tech.XBam.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Meep.Tech.XBam.IO.Configuration {

  /// <summary>
  /// A context importing and exporting model save data.
  /// </summary>
  public abstract class ModelIOContext : Universe.ExtraContext {
    JsonSerializerSettings _saveDataSerializerSettings;
    JsonSerializer _saveDataSerializer;

    static readonly MethodInfo _tryToGetBase
      = typeof(BuilderExtensions)
        .GetMethods()
        .First(m => m.Name == nameof(BuilderExtensions.TryToGet) && m.GetParameters()[1].ParameterType == typeof(string));

    readonly MethodInfo _stringGetter
       = _tryToGetBase
          .MakeGenericMethod(typeof(string));
    readonly MethodInfo _stringEnumerableGetter
       = _tryToGetBase
        .MakeGenericMethod(typeof(IEnumerable<object>));
    readonly MethodInfo _stringDictionaryGetter
       = _tryToGetBase
        .MakeGenericMethod(typeof(Dictionary<string, object>));
    readonly MethodInfo _stringEnumerableBackupGetter
       = _tryToGetBase
        .MakeGenericMethod(typeof(IEnumerable<string>));
    readonly MethodInfo _stringDictionaryBackupGetter
       = _tryToGetBase
        .MakeGenericMethod(typeof(Dictionary<string, string>));

    /// <summary>
    /// Save data json serializer.
    /// </summary>
    protected JsonSerializer SaveDataSerializer
      => _saveDataSerializer ??= JsonSerializer.Create(SaveDataSerializerSettings);

    /// <summary>
    /// Settings for save data json serialization
    /// </summary>
    protected JsonSerializerSettings SaveDataSerializerSettings 
      => _saveDataSerializerSettings ??= Universe.Loader.Options.ModelSerializerOptions.ConstructJsonSerializerSettings(
        new ModelPortingSaveJsonContract(Universe),
        Universe.Loader.Options.ModelSerializerOptions.DefaultJsonCoverters
      );

    /// <summary>
    /// Make a new model import context
    /// </summary>
    public ModelIOContext() {}

    /// <summary>
    /// Overrideable logic to fetch a model from cache or other stored location by it's ID.
    /// </summary>
    protected abstract bool TryToFetchModelByTypeAndId(System.Type type, string uniqueId, out IUnique? model, out Exception? error);

    /// <summary>
    /// Overrideable logic to fetch a model from cache or other stored location by it's ID.
    /// </summary>
    bool _tryToFetchModelByTypeAndId(System.Type type, string uniqueId, out IUnique model, out Exception error) {
      if (ICached.TryToGetFromCache(uniqueId, out model)) {
        error = null;
        return true;
      }
      else return TryToFetchModelByTypeAndId(type, uniqueId, out model, out error);
    }

    /// <summary>
    /// Update the copy settings to use the internal serializer
    /// </summary>
    protected override Action<Universe> OnLoaderInitializationComplete =>
      universe => {
        universe.Loader.Options.ModelSerializerOptions.DefaultCopyMethod =
          model => IModel.FromJson(model.ToJson(SaveDataSerializer));
      };

    /// <summary>
    /// Adds auto porting steps for auto building.
    /// </summary>
    protected override Action<Type, AutoBuildAttribute, PropertyInfo> OnLoaderAutoBuildPropertyCreationStart
      => (modelType, autoBuildData, fieldInfo) => {
        AutoPortAttribute autoPortAttribute;
        if ((autoPortAttribute = fieldInfo.GetCustomAttribute<AutoPortAttribute>(true)) != null) {

          // check for skipping
          if (autoPortAttribute.IgnoreDuringAutoBuilding) {
            return;
          }

          // if not, these types get a custom getter
          autoBuildData.Getter
            = new((m, b, p, a, r) => {
              MethodInfo getterFunc = p.PropertyType.IsAssignableToGeneric(typeof(IReadOnlyDictionary<,>))
                ? _stringDictionaryGetter
                : p.PropertyType.IsAssignableToGeneric(typeof(IEnumerable<>))
                  ? _stringEnumerableGetter
                  : _stringGetter;

              return AutoBuildAttribute.BuildDefaultGetterFromBuilderOrDefault(m, b, p, a, (m, b, p, a, r) => {
                object[] parameters = new object[] { b, a.ParameterName ?? p.Name, null, default };

                if ((bool)getterFunc.Invoke(null, parameters)) {
                  // if we succeeded in our get and it's a dictionary type field:
                  if (getterFunc == _stringDictionaryGetter) {
                    // if we need to preserve the keys
                    if (autoPortAttribute.PreserveKeys) {
                      return _getDictionaryWithPreservedKeysFromDictionary(fieldInfo, parameters);
                    } // if the keys are just the item ids
                    else {
                      return _getDictionaryFromDictionary(fieldInfo, parameters);
                    }
                  } // if ifs for an enumerable type
                  else if (getterFunc == _stringEnumerableGetter) {
                    return _getForEnumerableFromEnumerable(fieldInfo, parameters);
                  } // if we succeeded for a regular single model
                  else {
                    return parameters[2] is string id
                        ? _tryToFetchModelByTypeAndId(p.PropertyType, id, out var model, out var error)
                          ? model
                          : throw error
                        : parameters[2];
                  }
                } // if we failed and it's a dictionary getter, try the backup
                else if (getterFunc == _stringDictionaryGetter) {
                  // if the backup succeeds
                  if ((bool)_stringDictionaryBackupGetter.Invoke(null, parameters)) {
                    // if we need to preserve the keys
                    if (autoPortAttribute.PreserveKeys) {
                      return _getDictionaryWithPreservedKeysFromDictionaryBackup(fieldInfo, parameters);
                    } // if the keys are just the item ids
                    else {
                      return _getDictionaryFromDictionaryBackup(fieldInfo, parameters);
                    }
                  }

                  // if we failed the backup but have preserve keys off, we can try to get and adapt an enumerable.
                  if (!autoPortAttribute.PreserveKeys) {
                    // backup to use IE<object> in place of ID<string, object>
                    if (!(bool)_stringEnumerableGetter.Invoke(null, parameters)) {
                      // backup for IE<string>
                      if ((bool)_stringEnumerableBackupGetter.Invoke(null, parameters)) {
                        return _getDictionaryFromEnumerableBackup(fieldInfo, parameters);
                      }
                    }
                    else {
                      return _getDictionaryFromEnumerable(fieldInfo, parameters);
                    }
                  }
                }
                else if (getterFunc == _stringEnumerableGetter) {
                  if ((bool)_stringEnumerableBackupGetter.Invoke(null, parameters)) {
                    return _getFromEnumerableBackup(fieldInfo, parameters);
                  }
                }

                return !r
                  ? AutoBuildAttribute.GetDefaultValue(m, b, p, a, r)
                  : throw new IModel.IBuilder.Param.MissingException($"Missing param of type {p.PropertyType} or portable id for that type as a param with the name:{a.ParameterName ?? p.Name}");
              });
            });
        }
      };

    object _getDictionaryFromDictionaryBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (KeyValuePair<string, string> entry in parameters[2] as Dictionary<string, string>) {
        if (_tryToFetchModelByTypeAndId(modelToPortType, entry.Value, out var model, out var error)) {
          result.Add(model.Id, model);
        }
        else throw error;
      }

      return result;
    }

    object _getDictionaryWithPreservedKeysFromDictionaryBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, string>) {
        if (_tryToFetchModelByTypeAndId(modelToPortType, entry.Value, out var model, out var error)) {
          result.Add(entry.Key, model);
        }
        else throw error;
      }

      return result;
    }

    object _getDictionaryFromDictionary(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, object>) {
        object value = entry.Value is string
          ? _tryToFetchModelByTypeAndId(modelToPortType, entry.Value as string, out var model, out var error)
            ? model
            : throw error
          : entry.Value;
        result.Add((value as IUnique).Id, value);
      }

      return result;
    }

    object _getDictionaryWithPreservedKeysFromDictionary(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[1];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as Dictionary<string, object>) {
        object value = entry.Value is string
          ? _tryToFetchModelByTypeAndId(modelToPortType, entry.Value as string, out var model, out var error)
            ? model
            : throw error
          : entry.Value;
        result.Add(entry.Key, value);
      }

      return result;
    }

    object _getDictionaryFromEnumerableBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as IEnumerable<string>) {
        if (_tryToFetchModelByTypeAndId(modelToPortType, entry, out var model, out var error)) {
          result.Add(model.Id, model);
        }
        else throw error;
      }

      return result;
    }

    object _getDictionaryFromEnumerable(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IDictionary result = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), modelToPortType)) as IDictionary;
      foreach (var entry in parameters[2] as IEnumerable<object>) {
        object value = entry is string
          ? _tryToFetchModelByTypeAndId(modelToPortType, entry as string, out var model, out var error)
            ? model
            : throw error
          : entry;
        result.Add((entry as IUnique).Id, value);
      }

      return result;
    }

    object _getFromEnumerableBackup(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IList result = Activator.CreateInstance(typeof(List<>).MakeGenericType(modelToPortType)) as IList;
      foreach (var entry in parameters[2] as IEnumerable<string>) {
        if (_tryToFetchModelByTypeAndId(modelToPortType, entry, out var model, out var error)) {
          result.Add(model);
        }
        else throw error;
      }

      return result;
    }

    object _getForEnumerableFromEnumerable(PropertyInfo fieldInfo, object[] parameters) {
      Type modelToPortType = fieldInfo.DeclaringType.GenericTypeArguments[0];
      IList result = Activator.CreateInstance(typeof(List<>).MakeGenericType(modelToPortType)) as IList;
      foreach (var entry in parameters[2] as IEnumerable<object>) {
        object value = entry is string
          ? _tryToFetchModelByTypeAndId(modelToPortType, entry as string, out var model, out var error)
            ? model
            : throw error
          : entry;
        result.Add(value);
      }

      return result;
    }
  }
}
