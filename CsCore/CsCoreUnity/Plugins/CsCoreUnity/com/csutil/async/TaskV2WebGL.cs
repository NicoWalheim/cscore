﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace com.csutil.async {

    class TaskV2WebGL : TaskV2 {

        protected override Task DelayTask(int millisecondsDelay) {
            return StartCoroutineAsTask(DelayCoroutine(millisecondsDelay));
        }

        private IEnumerator DelayCoroutine(int ms) { yield return new WaitForSeconds(ms / 1000f); }

        protected override Task RunTask(Action action) {
            return StartCoroutineAsTask(RunCoroutine(action));
        }

        private IEnumerator RunCoroutine(Action action) {
            yield return new WaitForEndOfFrame();
            action();
        }

        protected override Task RunTask(Func<Task> asyncAction) {
            return StartCoroutineAsTask(RunCoroutine(asyncAction));
        }

        private static Task StartCoroutineAsTask(IEnumerator iEnum) {
            var tcs = new TaskCompletionSource<bool>();
            MainThread.Invoke(() => { MainThread.instance.StartCoroutineAsTask(tcs, iEnum, () => true); });
            return tcs.Task;
        }

        private IEnumerator RunCoroutine(Func<Task> action) {
            yield return new WaitForEndOfFrame();
            yield return action().AsCoroutine();
        }

    }

}
