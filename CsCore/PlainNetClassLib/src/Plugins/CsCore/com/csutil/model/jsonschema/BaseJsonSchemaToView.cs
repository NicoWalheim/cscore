﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace com.csutil.model.jsonschema {

    /// <summary> An abstract generator that can create a view from an input json schema </summary>
    /// <typeparam name="V">The view type, in Unity views for example are made out of GameObjects </typeparam>
    public abstract class BaseJsonSchemaToView<V> {

        public ModelToJsonSchema schemaGenerator;

        public BaseJsonSchemaToView(ModelToJsonSchema schemaGenerator) {
            this.schemaGenerator = schemaGenerator;
        }

        public async Task<V> ToView(JsonSchema rootSchema) {
            var rootView = await NewRootContainerView();
            await InitChild(rootView, null, rootSchema);
            await ObjectJsonSchemaToView(rootSchema, await SelectInnerViewContainerFromObjectFieldView(rootView));
            return rootView;
        }

        public async Task ObjectJsonSchemaToView(JsonSchema schema, V parentView) {
            foreach (var fieldName in schema.GetOrder()) {
                JsonSchema field = schema.properties[fieldName];
                await AddViewForJsonSchemaField(parentView, field, fieldName);
            }
        }

        public async Task<V> AddViewForJsonSchemaField(V parentView, JsonSchema field, string fieldName) {
            JTokenType type = field.GetJTokenType();
            if (type == JTokenType.Boolean) {
                var c = await AddChild(parentView, await NewBoolFieldView(field));
                await InitChild(c, fieldName, field);
                return c;
            }
            if (type == JTokenType.Integer) {
                if (!field.contentEnum.IsNullOrEmpty()) {
                    var c = await AddChild(parentView, await NewEnumFieldView(field));
                    await InitChild(c, fieldName, field);
                    return c;

                } else {
                    var c = await AddChild(parentView, await NewIntegerFieldView(field));
                    await InitChild(c, fieldName, field);
                    return c;
                }
            }
            if (type == JTokenType.Float) {
                var c = await AddChild(parentView, await NewFloatFieldView(field));
                await InitChild(c, fieldName, field);
                return c;
            }
            if (type == JTokenType.String) {
                if (!field.contentEnum.IsNullOrEmpty()) {
                    var c = await AddChild(parentView, await NewEnumFieldView(field));
                    await InitChild(c, fieldName, field);
                    return c;
                } else {
                    var c = await AddChild(parentView, await NewStringFieldView(field));
                    await InitChild(c, fieldName, field);
                    return c;
                }
            }
            if (type == JTokenType.Object) {
                if (field.properties == null) {
                    return await HandleRecursiveSchema(parentView, fieldName, field, schemaGenerator.schemas.GetValue(field.modelType, null));
                } else {
                    var objectFieldView = await NewObjectFieldView(field);
                    await InitChild(await AddChild(parentView, objectFieldView), fieldName, field);
                    await ObjectJsonSchemaToView(field, await SelectInnerViewContainerFromObjectFieldView(objectFieldView));
                    return objectFieldView;
                }
            }
            if (type == JTokenType.Array) {
                var e = field.items;
                if (e.Count == 1) {
                    JsonSchema item = e.First();
                    var childJType = item.GetJTokenType();
                    if (schemaGenerator.IsSimpleType(childJType)) {
                        return await HandleSimpleArray(parentView, fieldName, field);
                    } else if (childJType == JTokenType.Object) {
                        return await HandleObjectArray(parentView, fieldName, field);
                    } else {
                        throw new NotImplementedException("Array handling not impl. for type " + item.type);
                    }
                } else {
                    return await HandleMixedObjectArray(parentView, fieldName, field);
                }
            }
            throw new NotImplementedException($"Did not handle field {field.title} of type={type}");
        }

        public abstract Task<V> AddChild(V parentView, V child);
        public abstract Task InitChild(V view, string fieldName, JsonSchema field);

        public abstract Task<V> NewRootContainerView();

        public abstract Task<V> SelectInnerViewContainerFromObjectFieldView(V objectFieldView);

        public abstract Task<V> NewBoolFieldView(JsonSchema field);
        public abstract Task<V> NewStringFieldView(JsonSchema field);
        public abstract Task<V> NewFloatFieldView(JsonSchema field);
        public abstract Task<V> NewIntegerFieldView(JsonSchema field);
        public abstract Task<V> NewEnumFieldView(JsonSchema field);
        public abstract Task<V> NewObjectFieldView(JsonSchema field);

        public abstract Task<V> HandleRecursiveSchema(V parentView, string fieldName, JsonSchema field, JsonSchema recursiveSchema);
        public abstract Task<V> HandleSimpleArray(V parentView, string fieldName, JsonSchema field);
        public abstract Task<V> HandleObjectArray(V parentView, string fieldName, JsonSchema field);
        public abstract Task<V> HandleMixedObjectArray(V parentView, string fieldName, JsonSchema field);

    }

}