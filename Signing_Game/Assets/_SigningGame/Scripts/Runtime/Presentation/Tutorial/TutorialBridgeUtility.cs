using System;
using Cysharp.Threading.Tasks;
using Services;
using Services.Locator;
using UnityEngine;

namespace Presentation.Tutorial {
    /// <summary>
    /// Waits until the scene service scope is ready and hands the resolved tutorial service to the
    /// binder callback. Bridges live only in the gameplay scene; in other scenes the callback never runs.
    /// </summary>
    internal static class TutorialBridgeUtility {
        internal static void BindWhenReady(MonoBehaviour component, Action<TutorialService> bind) {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (bind == null) throw new ArgumentNullException(nameof(bind));
            BindWhenReadyAsync(component, bind).Forget();
        }

        private static async UniTaskVoid BindWhenReadyAsync(MonoBehaviour component, Action<TutorialService> bind) {
            ServiceLocator locator = ServiceLocator.For(component);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: component.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            if (component == null || !locator.TryGet(out TutorialService tutorial)) return;
            bind(tutorial);
        }
    }
}
