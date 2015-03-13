using System;
using System.Collections.Generic;
using NetTopologySuite.Features;

namespace LandRush.IO.DMF
{
	public class AttributesTable : IAttributesTable
	{
		public AttributesTable(IDictionary<Attribute, object> attributesValues)
		{
			this.attributesValues = attributesValues;
		}

		public void AddAttribute(string attributeName, object value)
		{
			throw new System.NotSupportedException();
		}

		public void DeleteAttribute(string attributeName)
		{
			throw new System.NotSupportedException();
		}

		public Type GetType(string attributeName)
		{
			Attribute attribute = null;

			if (!this.TryGetAttributeByName(attributeName, out attribute))
			{
				throw new System.ArgumentException(String.Format("attribute with name {0} does not exist", attributeName));
			}

			return attribute.ValueType;
		}

		public object this[string attributeName]
		{
			get
			{
				Attribute attribute = null;

				if (!this.TryGetAttributeByName(attributeName, out attribute))
				{
					throw new System.ArgumentException(String.Format("attribute with name {0} does not exist", attributeName));
				}

				return this.attributesValues[attribute];
			}

			set { throw new System.NotSupportedException(); }
		}

		public bool Exists(string attributeName)
		{
			Attribute attribute = null;
			return this.TryGetAttributeByName(attributeName, out attribute);
		}

		public bool Exists(Attribute attribute)
		{
			return this.attributesValues.Keys.Contains(attribute);
		}

		public int Count
		{
			get { return this.attributesValues.Count; }
		}

		public string[] GetNames()
		{
			string[] names = new string[this.attributesValues.Count];
			uint attributeNumber = 0;

			foreach (Attribute attribute in this.attributesValues.Keys)
			{
				names[attributeNumber] = attribute.Name;
				++attributeNumber;
			}

			return names;
		}

		public object[] GetValues()
		{
			object[] values= new object[this.attributesValues.Values.Count];
			this.attributesValues.Values.CopyTo(values, 0);

			return values;
		}

		// returns first entry of attribute with attributeName
		// other entries if exist are ignored
		private bool TryGetAttributeByName(string attributeName, out Attribute result)
		{
			result = null;

			foreach (Attribute attribute in this.attributesValues.Keys)
			{
				if (attribute.Name == attributeName)
				{
					result = attribute;
					return true;
				}
			}

			return false;
		}

		private IDictionary<Attribute, object> attributesValues;
	}
}
