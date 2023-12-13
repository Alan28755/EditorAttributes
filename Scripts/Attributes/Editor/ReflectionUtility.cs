using System;
using UnityEditor;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace EditorAttributes.Editor
{
    public static class ReflectionUtility
    {
		public const BindingFlags BINDING_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

		public static FieldInfo FindField(string fieldName, SerializedProperty property)
		{
			var fieldInfo = FindField(fieldName, property.serializedObject.targetObject);

			// If the field null we try to see if its inside a serialized object
			if (fieldInfo == null)
			{
				var serializedObjectType = GetSerializedObjectFieldType(property, out _);

				if (serializedObjectType != null) fieldInfo = serializedObjectType.GetField(fieldName, BINDING_FLAGS);
			}

			return fieldInfo;
		}

		internal static FieldInfo FindField(string fieldName, object targetObject) => FindMember(fieldName, targetObject.GetType(), BINDING_FLAGS, MemberTypes.Field, 5) as FieldInfo;

		public static PropertyInfo FindProperty(string propertyName, SerializedProperty property)
		{
			var propertyInfo = FindProperty(propertyName, property.serializedObject.targetObject);

			// If the property null we try to see if its inside a serialized object
			if (propertyInfo == null)
			{
				var serializedObjectType = GetSerializedObjectFieldType(property, out _);

				if (serializedObjectType != null) propertyInfo = serializedObjectType.GetProperty(propertyName, BINDING_FLAGS);
			}

			return propertyInfo;
		}

		internal static PropertyInfo FindProperty(string propertyName, object targetObject) => FindMember(propertyName, targetObject.GetType(), BINDING_FLAGS, MemberTypes.Property, 5) as PropertyInfo;

		public static MethodInfo FindFunction(string functionName, SerializedProperty property)
		{
			MethodInfo methodInfo;

			methodInfo = FindFunction(functionName, property.serializedObject.targetObject);

			// If the method is null we try to see if its inside a serialized object
			if (methodInfo == null)
			{
				var serializedObjectType = GetSerializedObjectFieldType(property, out _);

				try
				{
					methodInfo = serializedObjectType.GetMethod(functionName, BINDING_FLAGS);
				}
				catch (AmbiguousMatchException)
				{
					var functions = serializedObjectType.GetMethods();

					foreach (var function in functions)
					{
						if (function.Name == functionName) methodInfo = function;
					}
				}
			}

			return methodInfo;
		}

		internal static MethodInfo FindFunction(string functionName, object targetObject)
		{
			try
			{
				return FindMember(functionName, targetObject.GetType(), BINDING_FLAGS, MemberTypes.Method, 5) as MethodInfo;
			}
			catch (AmbiguousMatchException)
			{
				var functions = targetObject.GetType().GetMethods();

				foreach (var function in functions)
				{
					if (function.Name == functionName) return function;
				}

				return null;
			}
		}

		public static MemberInfo FindMember(string memberName, Type targetType, BindingFlags bindingFlags, MemberTypes memberType, int maxIterations)
		{
			switch (memberType)
			{
				case MemberTypes.Field:

					FieldInfo fieldInfo = null;

					for (int i = maxIterations; i > 0 && targetType != null && !TryGetField(memberName, targetType, bindingFlags, out fieldInfo); i--) 
						targetType = targetType.BaseType;

					return fieldInfo;

				case MemberTypes.Property:

					PropertyInfo propertyInfo = null;

					for (int i = maxIterations; i > 0 && targetType != null && !TryGetProperty(memberName, targetType, bindingFlags, out propertyInfo); i--) 
						targetType = targetType.BaseType;

					return propertyInfo;

				case MemberTypes.Method:

					MethodInfo methodInfo = null;

					for (int i = maxIterations; i > 0 && targetType != null && !TryGetMethod(memberName, targetType, bindingFlags, out methodInfo); i--) 
						targetType = targetType.BaseType;

					return methodInfo;
			}

			return null;
		}

		public static bool TryGetField(string name, Type type, BindingFlags bindingFlags, out FieldInfo fieldInfo)
		{
			fieldInfo = type.GetField(name, bindingFlags);

			return fieldInfo != null;
		}

		public static bool TryGetProperty(string name, Type type, BindingFlags bindingFlags, out PropertyInfo propertyInfo)
		{
			propertyInfo = type.GetProperty(name, bindingFlags);

			return propertyInfo != null;
		}

		public static bool TryGetMethod(string name, Type type, BindingFlags bindingFlags, out MethodInfo methodInfo)
		{
			methodInfo = type.GetMethod(name, bindingFlags);

			return methodInfo != null;
		}

		public static bool IsPropertyCollection(SerializedProperty property)
		{
			var arrayField = FindField(property.propertyPath.Split(".")[0], property);
			var memberInfoType = GetMemberInfoType(arrayField);

			return memberInfoType.IsArray || memberInfoType.GetInterfaces().Contains(typeof(IList));
		}

		public static MemberInfo GetValidMemberInfo(string parameterName, SerializedProperty serializedProperty)
		{
			MemberInfo memberInfo;

			memberInfo = FindField(parameterName, serializedProperty);

			memberInfo ??= FindProperty(parameterName, serializedProperty);
			memberInfo ??= FindFunction(parameterName, serializedProperty);

			return memberInfo;
		}

		internal static MemberInfo GetValidMemberInfo(string parameterName, object targetObject) // Internal function used for the button drawer
		{
			MemberInfo memberInfo;

			memberInfo = FindField(parameterName, targetObject);

			memberInfo ??= FindProperty(parameterName, targetObject);
			memberInfo ??= FindFunction(parameterName, targetObject);

			return memberInfo;
		}

		public static Type GetSerializedObjectFieldType(SerializedProperty property, out object serializedObject)
		{
			var targetObject = property.serializedObject.targetObject;
			var pathComponents = property.propertyPath.Split('.'); // Split the property path to get individual components
			var targetObjectType = targetObject.GetType();

			var serializedObjectField = FindMember(pathComponents[0], targetObjectType, BINDING_FLAGS, MemberTypes.Field, 5) as FieldInfo;

			serializedObject = serializedObjectField.GetValue(targetObject);

			return serializedObject?.GetType();
		}

		public static Type GetMemberInfoType(MemberInfo memberInfo)
		{
			if (memberInfo is FieldInfo fieldInfo)
			{
				return fieldInfo.FieldType;
			}
			else if (memberInfo is PropertyInfo propertyInfo)
			{
				return propertyInfo.PropertyType;
			}
			else if (memberInfo is MethodInfo methodInfo)
			{
				return methodInfo.ReturnType;
			}

			return null;
		}

		public static object GetMemberInfoValue(MemberInfo memberInfo, SerializedProperty property)
		{
			var targetObject = property.serializedObject.targetObject;

			try
			{
				if (memberInfo is FieldInfo fieldInfo)
				{
					return fieldInfo.GetValue(targetObject);
				}
				else if (memberInfo is PropertyInfo propertyInfo)
				{
					return propertyInfo.GetValue(targetObject);
				}
				else if (memberInfo is MethodInfo methodInfo)
				{
					return methodInfo.Invoke(targetObject, null);
				}
			}
			catch (ArgumentException) // If this expection is thrown it means that the member we try to get the value from is inside a different target
			{
				GetSerializedObjectFieldType(property, out object serializedObjectTarget);

				if (memberInfo is FieldInfo fieldInfo)
				{
					return fieldInfo.GetValue(serializedObjectTarget);
				}
				else if (memberInfo is PropertyInfo propertyInfo)
				{
					return propertyInfo.GetValue(serializedObjectTarget);
				}
				else if (memberInfo is MethodInfo methodInfo)
				{
					return methodInfo.Invoke(serializedObjectTarget, null);
				}
			}

			return null;
		}

		internal static object GetMemberInfoValue(MemberInfo memberInfo, object targetObject) // Internal function used for the button drawer
		{
			if (memberInfo is FieldInfo fieldInfo)
			{
				return fieldInfo.GetValue(targetObject);
			}
			else if (memberInfo is PropertyInfo propertyInfo)
			{
				return propertyInfo.GetValue(targetObject);
			}
			else if (memberInfo is MethodInfo methodInfo)
			{
				return methodInfo.Invoke(targetObject, null);
			}

			return null;
		}
	}
}
