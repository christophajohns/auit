using System.Collections;
using UnityEngine;

namespace AUIT.AdaptationTriggers
{
    public class ContinuousOptimizationTrigger : AdaptationTrigger
    {
        [SerializeField, Tooltip("Running asynchronously will spread computations over multiple frames, making the result come later but not heavily impact framerate.")]
        private bool runAsynchronous = false;

        [Header("Thresholds")]
        [SerializeField]
        private float optimizationThreshold = 0.05f;
        [SerializeField]
        private float adaptationThreshold = 0.1f;

        [Header("Limit optimization run time")]
        [SerializeField]
        private float optimizationTimeout = 5.0f;
        private float optimizationTimeStart = 0.0f;

        private float previousCost;

        protected void Start()
        {
            StartCoroutine(ApplyContinuously());
        }

        private IEnumerator ApplyContinuously()
        {
            yield return new WaitForSecondsRealtime(0.5f);

            while (true)
            {
                if (enabled == false)
                {
                    yield break;
                }

                ApplyStrategy();

                yield return new WaitForSecondsRealtime(.5f);
            }
        }

        private bool ShouldApplyAdaptation()
        {
            bool costIsBelowOptiThreshold;
            if (runAsynchronous)
            {
                if (AdaptationManager.AsyncComputeCost() == null)
                    return false;

                
                previousCost = AdaptationManager.AsyncComputeCost().Value;
                costIsBelowOptiThreshold = previousCost <= optimizationThreshold;
            }
            else
            {
                previousCost = AdaptationManager.ComputeCost();
                costIsBelowOptiThreshold = previousCost <= optimizationThreshold;
            }

            return enabled && !costIsBelowOptiThreshold && !AdaptationManager.IsAdapting;
        }

        public override void ApplyStrategy()
        {
            if (AdaptationManager.isActiveAndEnabled == false)
                return;
                
            if (!ShouldApplyAdaptation())
                return;

            if (runAsynchronous)
            {
                StartCoroutine(WaitForOptimizedLayout());
                return;
            }

            var (layouts, cost) = AdaptationManager.OptimizeLayout();

            bool shouldAdapt = previousCost - cost > adaptationThreshold;
            print($"cost diff: {previousCost - cost}");
            if (shouldAdapt)
            {
                if (AdaptationManager.isGlobal)
                {
                    for (int i = 0; i < AdaptationManager.UIElements.Count; i++)
                    {
                        AdaptationManager.UIElements[i].GetComponent<AdaptationManager>().layout = layouts[i];
                        AdaptationManager.UIElements[i].GetComponent<AdaptationManager>().Adapt(layouts[i]);
                    }
                }
                else
                {
                    AdaptationManager.layout = layouts[0];
                    AdaptationManager.Adapt(layouts[0]);
                }
            }
        }

        private IEnumerator WaitForOptimizedLayout()
        {
            optimizationTimeStart = Time.realtimeSinceStartup;

            while (true)
            {
                bool timeExceeded = Time.realtimeSinceStartup - optimizationTimeStart >= optimizationTimeout;
                if (this.enabled == false || timeExceeded || AdaptationManager.IsAdapting)
                    yield break;

                var (layouts, cost, previousCost) = AdaptationManager.AsyncOptimizeLayout();
                if (layouts != null && layouts.Count != 0)
                {
                    bool shouldAdapt = previousCost - cost > adaptationThreshold;
                    if (shouldAdapt)
                    {
                        if (AdaptationManager.isGlobal)
                        {
                            for (int i = 0; i < AdaptationManager.UIElements.Count; i++)
                            {
                                AdaptationManager.UIElements[i].GetComponent<AdaptationManager>().layout = layouts[i];
                                AdaptationManager.UIElements[i].GetComponent<AdaptationManager>().Adapt(layouts[i]);
                            }
                        }
                        else
                        {
                            AdaptationManager.layout = layouts[0];
                            AdaptationManager.Adapt(layouts[0]);
                        }
                        yield break;
                    }
                }
                yield return new WaitForEndOfFrame();
            }
        }
    }
}