﻿using com.csutil.model.mtvmtv;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace com.csutil.ui.mtvmtv {

    public static class ViewModelJsonExtensions {

        public static async Task LinkToJsonModel(this GameObject targetView, JObject root, ViewModelToView vmtv) {
            foreach (var fieldView in targetView.GetFieldViewMap().Values) {
                var value = fieldView.GetFieldJModel(root);
                if (!fieldView.LinkToJsonModel(root, value)) {
                    if (fieldView is RecursiveFieldView r) {
                        r.ShowChildModelInNewScreen(targetView, value as JObject);
                    } else if (fieldView is ObjectFieldView) {
                        // Do nothing (object fields are individually set up themselves)
                    } else if (fieldView is ListFieldView l) {
                        await l.LoadModelList(root, vmtv);
                    } else {
                        Log.e($"Did not link {fieldView.GetType()}: {fieldView.fullPath}");
                    }
                }
            }
        }

        public static bool LinkToJsonModel(this FieldView self, JObject root, JToken value) {
            if (self is EnumFieldView enumFieldView && value?.Type == JTokenType.Integer) {
                int posInEnum = int.Parse("" + value);
                var enumValues = self.field.contentEnum;
                enumFieldView.LinkToModel(enumValues[posInEnum], newVal => {
                    var newPosInEnum = Array.FindIndex(enumValues, x => x == newVal);
                    var jValueParent = self.CreateJValueParentsIfNeeded(root);
                    if (value is JValue v) {
                        v.Value = newPosInEnum;
                    } else {
                        value = new JValue(newPosInEnum);
                        jValueParent[self.fieldName] = value;
                    }
                });
                return true;
            }
            if (self is InputFieldView inputFieldView) {
                inputFieldView.LinkToModel("" + value, newVal => {
                    try {
                        var newJVal = self.field.ParseToJValue(newVal);
                        var jValueParent = self.CreateJValueParentsIfNeeded(root);
                        if (value is JValue v) {
                            v.Value = newJVal.Value;
                        } else {
                            value = newJVal;
                            jValueParent[self.fieldName] = value;
                        }
                    } // Ignore errors like e.g. FormatException when "" is parsed to int:
                    catch (FormatException e) { Log.w("" + e, self.gameObject); }
                });
                return true;
            }
            if (self is BoolFieldView boolFieldView) {
                bool val = (value as JValue)?.Value<bool>() == true;
                boolFieldView.LinkToModel(val, newB => {
                    var jValueParent = self.CreateJValueParentsIfNeeded(root);
                    if (value is JValue v) {
                        v.Value = newB;
                    } else {
                        value = new JValue(newB);
                        jValueParent[self.fieldName] = value;
                    }
                });
                return true;
            }
            if (self.field.readOnly == true) {
                self.LinkToModel("" + value);
                return true;
            }
            return false;
        }

        public static JToken GetFieldJModel(this FieldView self, JObject root) {
            JToken jParent = self.GetJParent(root);
            if (jParent is JArray) { return jParent[int.Parse(self.fieldName)]; }
            return jParent?[self.fieldName];
        }

        public static void ShowChildModelInNewScreen(this RecursiveFieldView self, GameObject currentScreen, JObject jObj) {
            self.openButton.SetOnClickAction(async delegate {
                var newScreen = await self.NewViewFromViewModel();
                var viewStack = currentScreen.GetViewStack();
                viewStack.ShowView(newScreen, currentScreen);
                var presenter = new JObjectPresenter(self.viewModelToView);
                presenter.targetView = newScreen;
                await presenter.LoadModelIntoView(jObj);
            }).LogOnError();
        }

        public static async Task LoadModelList(this ListFieldView self, JObject root, ViewModelToView vmtv) {
            JArray modelArray = self.GetFieldJModel(root) as JArray;
            AssertV2.IsNotNull(modelArray, "modelArray");
            var map = new Dictionary<FieldView, JToken>();
            for (int i = 0; i < modelArray.Count; i++) {
                var fieldName = "" + i;
                JToken entry = modelArray[i];
                var fv = await CreateChildEntryView(self, root, vmtv, entry, fieldName);
                map.Add(fv, entry);
            }
            SetupButtons(self, root, vmtv, modelArray, map);
        }

        private static void SetupButtons(ListFieldView listView, JObject root, ViewModelToView vmtv, JArray modelArray, Dictionary<FieldView, JToken> map) {
            listView.add.SetOnClickAction(async delegate {
                JToken entry = listView.field.items.First().NewDefaultJInstance();
                modelArray.Add(entry);
                var fieldName = "" + (modelArray.Count - 1);
                var fv = await CreateChildEntryView(listView, root, vmtv, entry, fieldName);
                map.Add(fv, entry);
            });
            listView.up.SetOnClickAction(delegate {
                foreach (var v in GetSelectedViews(listView)) {
                    var selectedData = map[v];
                    var index = modelArray.IndexOf(selectedData);
                    if (index > 0) {
                        modelArray.RemoveAt(index);
                        modelArray.Insert(index - 1, selectedData);
                        v.transform.SetSiblingIndex(v.transform.GetSiblingIndex() - 1);
                    }
                }
            });
            listView.down.SetOnClickAction(delegate {
                foreach (var v in GetSelectedViews(listView).Reverse()) {
                    var selectedData = map[v];
                    var index = modelArray.IndexOf(selectedData);
                    if (index < modelArray.Count - 1) {
                        modelArray.RemoveAt(index);
                        modelArray.Insert(index + 1, selectedData);
                        v.transform.SetSiblingIndex(v.transform.GetSiblingIndex() + 1);
                    }
                }
            });
            listView.delete.SetOnClickAction(delegate {
                foreach (var v in GetSelectedViews(listView)) {
                    var selectedData = map[v];
                    modelArray.Remove(selectedData);
                    v.gameObject.Destroy();
                }
            });
        }

        private static IEnumerable<ListEntryView> GetSelectedViews(ListFieldView self) {
            var entries = self.gameObject.GetComponentsInChildren<ListEntryView>();
            var checkedEntries = entries.Filter(x => x.checkmark.isOn);
            if (checkedEntries.IsNullOrEmpty()) { Toast.Show("No entries selected"); }
            return checkedEntries;
        }

        private static async Task<FieldView> CreateChildEntryView(
                ListFieldView self, JObject root, ViewModelToView vmtv, JToken modelEntry, string fieldName) {
            ViewModel newEntryVm = GetMatchingViewModel(modelEntry, self.field.items);
            GameObject childView = await AddChildEntryView(self, vmtv, fieldName, newEntryVm);
            await childView.LinkToJsonModel(root, vmtv);
            return childView.GetComponentInChildren<FieldView>();
        }

        private static ViewModel GetMatchingViewModel(JToken modelEntry, List<ViewModel> viewModels) {
            foreach (var vm in viewModels) { if (vm.GetJTokenType() == modelEntry.Type) { return vm; } }
            return null;
        }

        private static async Task<GameObject> AddChildEntryView(
                    ListFieldView self, ViewModelToView vmtv, string fieldName, ViewModel entryVm) {
            var parentView = self.mainLink.gameObject;
            if (CanBeShownInListViewEntry(entryVm.GetJTokenType())) {
                GameObject childGo = await vmtv.AddChild(parentView, await vmtv.NewListViewEntry());
                await vmtv.InitChild(childGo, fieldName, entryVm);
                return childGo;
            } else {
                return await vmtv.AddViewForFieldViewModel(parentView, entryVm, fieldName);
            }
        }

        private static bool CanBeShownInListViewEntry(JTokenType jType) {
            if (jType == JTokenType.Integer) { return true; }
            if (jType == JTokenType.Float) { return true; }
            if (jType == JTokenType.String) { return true; }
            return false;
        }

        private static JToken CreateJValueParentsIfNeeded(this FieldView self, JToken currentLevel) {
            if (self.IsInChildObject()) { // Navigate down to the correct child JObject
                string[] parents = self.fullPath.Split(".");
                for (int i = 0; i < parents.Length - 1; i++) {
                    string fieldName = parents.ElementAt(i);
                    var child = GetChildJToken(currentLevel, fieldName);
                    if (child == null) {
                        if (int.TryParse(parents.ElementAt(i + 1), out int _)) {
                            currentLevel[fieldName] = new JArray();
                        } else {
                            currentLevel[fieldName] = new JObject();
                        }
                    }
                    currentLevel = child;
                    AssertV2.NotNull(currentLevel, $"rootModel (p='{fieldName}', child={child}");
                }
            }
            return currentLevel;
        }

        public static JToken GetJParent(this FieldView self, JToken rootModel) {
            if (self.IsInChildObject()) { // Navigate down to the correct child JObject
                string[] parents = self.fullPath.Split(".");
                foreach (string parent in parents.Take(parents.Length - 1)) {
                    rootModel = GetChildJToken(rootModel, parent);
                }
            }
            return rootModel;
        }

        private static JToken GetChildJToken(JToken self, string entry) {
            if (int.TryParse(entry, out int i)) { // e.g. "user.friends.2.name"
                if (self is IEnumerable<JToken> list) {
                    return list.ElementAt(i);
                } else if (self is IEnumerable<KeyValuePair<string, JToken>> dict) {
                    return dict.ElementAt(i).Value;
                } else {
                    throw new NotImplementedException($"Could not get elem at pos {i} from {self}");
                }
            } else {
                return self[entry];
            }
        }

    }

}
