using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using PuzzleMind.Services; // Ваш namespace для моделей состояний
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PuzzleMind.Features
{
    public partial class WaterSortFeature : ComponentBase
    {
        [Inject]
        protected IStringLocalizer<WaterSortFeature> L { get; set; } = default!;

        protected WaterSortState gameState = WaterSortState.CreateEmpty(5, 4);
        protected int tubeCount = 5;
        protected bool isSolving = false;
        protected WaterSolver solver = new();
        protected int activeTabIndex = 0;

        // Переменные плеера
        protected List<WaterSortState> allStates = new();
        protected int currentStepIndex = 0;
        protected bool isPlaying = false;
        private CancellationTokenSource? animationCts;

        protected List<VisualStepInfo> uiSteps = new();

        public class VisualStepInfo
        {
            public int Index { get; set; }
            public int From { get; set; }
            public int To { get; set; }
            public string Color { get; set; } = "";
            public string Description { get; set; } = "";
        }

        protected override void OnInitialized()
        {
            ResetAllColors();
        }

        protected void UpdateTubeCount(int val)
        {
            tubeCount = val;
            var currentTubes = gameState.Tubes.ToList();

            if (currentTubes.Count < tubeCount)
            {
                while (currentTubes.Count < tubeCount)
                {
                    var newTube = Enumerable.Repeat("#eeeeee", gameState.Capacity).ToList();
                    currentTubes.Add(newTube);
                }
            }
            else if (currentTubes.Count > tubeCount)
            {
                currentTubes = currentTubes.Take(tubeCount).ToList();
            }

            gameState.Tubes = currentTubes;
        }

        protected void ResetAllColors()
        {
            StopAnimation();
            allStates.Clear();
            uiSteps.Clear();
            var emptyState = WaterSortState.CreateEmpty(tubeCount, 4);
            emptyState.Tubes = emptyState.Tubes
                .Select(_ => Enumerable.Repeat("#eeeeee", emptyState.Capacity).ToList())
                .ToList();
            gameState = emptyState;
            activeTabIndex = 0;
        }

        protected async Task RunSolver()
        {
            isSolving = true;
            StopAnimation();
            uiSteps.Clear();
            allStates.Clear();

            var cleanState = gameState.Clone();
            int maxFilledLayers = 0;

            for (int i = 0; i < cleanState.Tubes.Count; i++)
            {
                cleanState.Tubes[i] = cleanState.Tubes[i]
                    .Where(color => color != "#eeeeee" && !string.IsNullOrEmpty(color))
                    .ToList();

                if (cleanState.Tubes[i].Count > maxFilledLayers)
                {
                    maxFilledLayers = cleanState.Tubes[i].Count;
                }
            }

            if (maxFilledLayers == 0) { isSolving = false; return; }
            cleanState.Capacity = maxFilledLayers;

            var steps = await solver.SolveAsync(cleanState);

            if (steps != null)
            {
                GenerateUiSteps(steps);

                foreach (var step in steps)
                {
                    var visualStep = step.Clone();
                    visualStep.Capacity = 4;
                    for (int i = 0; i < visualStep.Tubes.Count; i++)
                    {
                        while (visualStep.Tubes[i].Count < 4)
                        {
                            visualStep.Tubes[i].Add("#eeeeee");
                        }
                    }
                    allStates.Add(visualStep);
                }

                activeTabIndex = 1;
                await PlayAnimation();
            }
            isSolving = false;
        }

        protected async Task PlayAnimation()
        {
            if (allStates.Count == 0) return;

            if (currentStepIndex >= allStates.Count - 1)
            {
                currentStepIndex = 0;
            }

            isPlaying = true;
            animationCts = new CancellationTokenSource();
            var token = animationCts.Token;

            try
            {
                while (currentStepIndex < allStates.Count && isPlaying)
                {
                    gameState = allStates[currentStepIndex];
                    StateHasChanged();

                    if (currentStepIndex == allStates.Count - 1)
                    {
                        break;
                    }

                    await Task.Delay(700, token);
                    currentStepIndex++;
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                isPlaying = false;
                StateHasChanged();
            }
        }

        protected void PauseAnimation()
        {
            isPlaying = false;
            animationCts?.Cancel();
            StateHasChanged();
        }

        protected void StopAnimation()
        {
            isPlaying = false;
            animationCts?.Cancel();
            currentStepIndex = 0;

            if (allStates.Count > 0)
            {
                gameState = allStates[0];
            }

            StateHasChanged();
        }

        protected void JumpToStep(int stepIndex)
        {
            PauseAnimation();

            if (stepIndex >= 0 && stepIndex < allStates.Count)
            {
                currentStepIndex = stepIndex;
                gameState = allStates[currentStepIndex];
                activeTabIndex = 1;
                StateHasChanged();
            }
        }

        private void GenerateUiSteps(List<WaterSortState> solution)
        {
            for (int k = 0; k < solution.Count - 1; k++)
            {
                var current = solution[k];
                var next = solution[k + 1];

                int from = -1, to = -1;
                for (int i = 0; i < current.Tubes.Count; i++)
                {
                    if (current.Tubes[i].Count > next.Tubes[i].Count) from = i;
                    if (current.Tubes[i].Count < next.Tubes[i].Count) to = i;
                }

                if (from != -1 && to != -1)
                {
                    string color = next.Tubes[to].Last();
                    uiSteps.Add(new VisualStepInfo
                    {
                        Index = k + 1,
                        From = from + 1,
                        To = to + 1,
                        Color = color,
                        // Форматируем локализованную строку, передавая номера колб
                        Description = string.Format(L["PourFromTo"], from + 1, to + 1)
                    });
                }
            }
        }
    }
}
