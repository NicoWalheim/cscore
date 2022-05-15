using System;
using System.Diagnostics;
using com.csutil.injection;

namespace com.csutil {

    public static class Singleton {

        private static object syncLock = new object();

        public static object SetSingleton<V>(this Injector self, V singletonInstance, bool overrideExisting = false) {
            return self.SetSingleton<V, V>(new object(), singletonInstance, overrideExisting);
        }

        public static V SetSingleton<V>(this Injector self, object caller, V singletonInstance, bool overrideExisting = false) {
            self.SetSingleton<V, V>(caller, singletonInstance, overrideExisting);
            return singletonInstance;
        }

        // private because normally prefer GetOrAddSingleton should be used instead
        private static object SetSingleton<T, V>(this Injector self, object caller, V singletonInstance, bool overrideExisting = false) where V : T {
            lock (syncLock) {
                singletonInstance.ThrowErrorIfNull("singletonInstance");
                if (self.HasInjectorRegistered<T>()) {
                    if (!overrideExisting) { throw new InvalidOperationException("Existing provider found for " + typeof(T)); }
                    if (!self.RemoveAllInjectorsFor<T>()) { Log.e("Could not remove all existing injectors!"); }
                    return SetSingleton<T, V>(self, caller, singletonInstance, false); // then retry setting the singleton
                }
                self.RegisterInjector<T>(caller, delegate {
                    if (singletonInstance is IsDisposable disposableObj && !disposableObj.IsAlive()) {
                        // If the found object is not active anymore destroy the singleton injector and return null
                        if (!self.RemoveAllInjectorsFor<T>()) { Log.e("Could not remove all existing injectors!"); }
                        return default;
                    }
                    return singletonInstance;
                });
                return caller;
            }
        }

        public static T GetOrAddSingleton<T>(this Injector self, object caller) {
            return GetOrAddSingleton(self, caller, () => CreateNewInstance<T>());
        }

        public static T GetOrAddSingleton<T>(this Injector self, object caller, Func<T> createSingletonInstance) {
            lock (syncLock) {
                if (self.TryGet(caller, out T singleton)) {
                    AssertNotNull(singleton);
                    return singleton;
                }
                singleton = createSingletonInstance();
                AssertNotNull(singleton);
                return self.SetSingleton(caller, singleton);
            }
        }

        [Conditional("DEBUG")]
        private static void AssertNotNull<T>(T singleton) {
            if (ReferenceEquals(null, singleton)) {
                throw new ArgumentNullException("The singleton instance was null for type " + typeof(T));
            }
            if ("null".Equals(singleton.ToString())) {
                throw new ArgumentNullException("The singleton instance returns 'null' in .ToString() for type " + typeof(T));
            }
        }

        private static T CreateNewInstance<T>() {
            return (T)Activator.CreateInstance(typeof(T));
        }

    }

}