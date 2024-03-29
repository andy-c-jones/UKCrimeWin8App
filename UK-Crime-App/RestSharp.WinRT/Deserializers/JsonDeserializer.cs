﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RestSharp.Extensions;
using RestSharp;
using System.Text;

namespace RestSharp.Deserializers
{
	public sealed class JsonDeserializer : IDeserializer
	{
		public string RootElement { get; set; }
		public string Namespace { get; set; }
		public string DateFormat { get; set; }

		public JsonDeserializer()
		{
		}

        public object Deserialize(IRestResponse response, Type type)
        {
            //Note: I think using GetRuntimeMethod here doesnt work because it only finds public members, so we have to be more brute force
            var methodInfo = this.GetType().GetTypeInfo().DeclaredMethods.Where(m => m.Name == "Deserialize" && m.IsGenericMethod).FirstOrDefault();
            var genericMethod = methodInfo.MakeGenericMethod(type);
            var result = genericMethod.Invoke(this, new object[] { response });

            return result;
        }

        private T Deserialize<T>(IRestResponse response)
        {
            var target = Activator.CreateInstance<T>();
        
            if (target is IList)
            {
                var objType = target.GetType();

                if (RootElement.HasValue())
                {
                    var root = FindRoot(response.Content);
                    target = (T)BuildList(objType, root);
                }
                else
                {
                    var data = SimpleJson.DeserializeObject(response.Content);
                    target = (T)BuildList(objType, data);
                }
            }
            else if (target is IDictionary)
            {
                var root = FindRoot(response.Content);
                target = (T)BuildDictionary(target.GetType(), root);
            }
            else
            {
                var root = FindRoot(response.Content);
                Map(target, (IDictionary<string, object>)root);
            }

            return target;
        }

        private object FindRoot(string content)
        {
            var data = (IDictionary<string, object>)SimpleJson.DeserializeObject(content);
            if (RootElement.HasValue() && data.ContainsKey(RootElement))
            {
                return data[RootElement];
            }
            return data;
        }

		private void Map(object target, IDictionary<string, object> data)
		{
			var objType = target.GetType();

			//var props = objType.GetProperties().Where(p => p.CanWrite).ToList();
            var props = System.Reflection.IntrospectionExtensions.GetTypeInfo(objType).DeclaredProperties.Where(p=>p.CanWrite).ToList();

			foreach (var prop in props)
			{
				var type = prop.PropertyType;

				var name = prop.Name;
				var actualName = name.GetNameVariants().FirstOrDefault(n => data.ContainsKey(n));
				var value = actualName != null ? data[actualName] : null;

				if (value == null) continue;

				// check for nullable and extract underlying type
				if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
				{
					//type = type.GetTypeInfo().GetGenericArguments()[0];
                    type = type.GetTypeInfo().GenericTypeArguments[0];
				}

				prop.SetValue(target, ConvertValue(type, value), null);
			}
		}

		private IDictionary BuildDictionary(Type type, object parent)
		{
			var dict = (IDictionary)Activator.CreateInstance(type);
			
            //var valueType = type.GetGenericArguments()[1];
            var valueType = type.GenericTypeArguments[1];

			foreach (var child in (IDictionary<string, object>)parent)
			{
				var key = child.Key;
				var item = ConvertValue(valueType, child.Value);
				dict.Add(key, item);
			}

			return dict;
		}

		private IList BuildList(Type type, object parent)
		{
            //HACK: if this is an interface, then default to its concrete implementation
            if (type.GetTypeInfo().IsInterface)            
            {
                if (type.Name.StartsWith("I"))
                {
                    var name = type.Name.Substring(1); //strip the 'I'

                    var newtype = Type.GetType(type.Namespace + "." + name);

                    if (type.GetTypeInfo().IsGenericType)
                    {
                        newtype = newtype.MakeGenericType(type.GenericTypeArguments);
                    }

                    type = newtype;
                }
            }

			var list = (IList)Activator.CreateInstance(type);
			//var listType = type.GetInterfaces().First(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
            //var itemType = listType.GetGenericArguments()[0];

            var listType = type.GetTypeInfo().ImplementedInterfaces.First(x => x.GetTypeInfo().IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
            var itemType = listType.GenericTypeArguments[0];

			if (parent is IList)
			{
				foreach (var element in (IList)parent)
				{
					if (itemType.GetTypeInfo().IsPrimitive)
					{
						var value = element.ToString();
						list.Add(value.ChangeType(itemType));
					}
					else if (itemType == typeof(string))
					{
						if (element == null)
						{
							list.Add(null);
							continue;
						}

						list.Add(element.ToString());
					}
					else
					{
						if (element == null)
						{
							list.Add(null);
							continue;
						}

						var item = ConvertValue(itemType, element);
						list.Add(item);
					}
				}
			}
			else
			{
				list.Add(ConvertValue(itemType, parent));
			}
			return list;
		}

		private object ConvertValue(Type type, object value)
		{
			var stringValue = Convert.ToString(value);

			if (type.GetTypeInfo().IsPrimitive)
			{
				// no primitives can contain quotes so we can safely remove them
				// allows converting a json value like {"index": "1"} to an int
				var tmpVal = stringValue.Replace("\"", string.Empty);

				return tmpVal.ChangeType(type);
			}
			//else if (type.IsEnum)
            else if (type.GetTypeInfo().IsEnum)
			{
				return type.FindEnumValue(stringValue);
			}
			else if (type == typeof(Uri))
			{
				return new Uri(stringValue, UriKind.RelativeOrAbsolute);
			}
			else if (type == typeof(string))
			{
				return stringValue;
			}
			else if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
			{
				DateTimeOffset dt;
				if (DateFormat.HasValue())
				{
					dt = DateTime.ParseExact(stringValue, DateFormat, null);
				}
				else
				{
					// try parsing instead
					dt = stringValue.ParseJsonDate();
				}

				if (type == typeof(DateTime))
				{
					return dt;
				}
				else if (type == typeof(DateTimeOffset))
				{
					return (DateTimeOffset)dt;
				}
			}
			else if (type == typeof(Decimal))
			{
				return Decimal.Parse(stringValue);
			}
			else if (type == typeof(Guid))
			{
				return string.IsNullOrEmpty(stringValue) ? Guid.Empty : new Guid(stringValue);
			}
			else if (type == typeof(TimeSpan))
			{
				return TimeSpan.Parse(stringValue);
			}
			//else if (type.IsGenericType)
            else if (System.Reflection.IntrospectionExtensions.GetTypeInfo(type).IsGenericType)
			{
				var genericTypeDef = type.GetGenericTypeDefinition();
				if (genericTypeDef == typeof(IList<>))
				{
					return BuildList(type, value);
				}
				else if (genericTypeDef == typeof(Dictionary<,>))
				{
					//var keyType = type.GetGenericArguments()[0];
                    var keyType = type.GenericTypeArguments[0];

					// only supports Dict<string, T>()
					if (keyType == typeof(string))
					{
						return BuildDictionary(type, value);
					}
				}
				else
				{
					// nested property classes
					return CreateAndMap(type, value);
				}
			}
			else
			{
				// nested property classes
				return CreateAndMap(type, value);
			}

			return null;
		}

		private object CreateAndMap(Type type, object element)
		{
			var instance = Activator.CreateInstance(type);

			Map(instance, (IDictionary<string, object>)element);

			return instance;
		}
	}
}