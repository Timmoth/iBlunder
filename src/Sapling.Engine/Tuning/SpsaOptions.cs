using Sapling.Engine.Search;
using System.Reflection;

namespace Sapling.Engine.Tuning
{
    public class SpsaParameter
    {
        public string Name;
        public string Type;
        public string DefaultValue;
        public string MinValue;
        public string MaxValue;
        public FieldInfo FieldHandle;
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class SpsaMinValueAttribute : Attribute
    {
        public string MinValue { get; }

        public SpsaMinValueAttribute(string minValue)
        {
            MinValue = minValue;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class SpsaIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class SpsaMaxValueAttribute : Attribute
    {
        public string MaxValue { get; }

        public SpsaMaxValueAttribute(string maxValue)
        {
            MaxValue = maxValue;
        }
    }


    public static unsafe class SpsaOptions
    {
#if OpenBench
        [SpsaMinValue("60"), SpsaMaxValue("80")]
        public static int ReverseFutilityPruningMargin = 68;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int ReverseFutilityPruningDepth = 7;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int NullMovePruningDepth = 2;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static float NullMovePruningReductionA = 3;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static float NullMovePruningReductionB = 4;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static float NullMovePruningReductionC = 3;

        [SpsaMinValue("50"), SpsaMaxValue("100")]
        public static int RazorMarginA = 57;

        [SpsaMinValue("250"), SpsaMaxValue("400")]
        public static int RazorMarginB = 370;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int InternalIterativeDeepeningDepth = 2;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int LateMovePruningConstant = 8;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int LateMoveReductionMinDepth = 3;

        [SpsaMinValue("0"), SpsaMaxValue("10")]
        public static int LateMoveReductionMinMoves = 2;

        [SpsaMinValueAttribute("0.1"), SpsaMaxValue("10")]
        public static float LateMoveReductionInterestingA = 0.188786f;

        [SpsaMinValueAttribute("0.1"), SpsaMaxValue("10")]
        public static float LateMoveReductionInterestingB = 2.4956496f;

        [SpsaMinValueAttribute("0.1"), SpsaMaxValue("10")]
        public static float LateMoveReductionA = 1.315961f;

        [SpsaMinValueAttribute("0.1"), SpsaMaxValue("10")]
        public static float LateMoveReductionB = 2.7831153f;

        [SpsaMinValue("7000"), SpsaMaxValue("10000")]
        public static int HistoryHeuristicMaxHistory = 9599;

        [SpsaMinValue("256"), SpsaMaxValue("1000")]
        public static int HistoryHeuristicBonusMax = 425;

        [SpsaMinValue("50"), SpsaMaxValue("256")]
        public static int HistoryHeuristicBonusCoeff = 86;

        [SpsaMinValue("100000"), SpsaMaxValue("300000")]
        public static int MoveOrderingBestMoveBias = 240615;

        [SpsaMinValue("50000"), SpsaMaxValue("100000")]
        public static int MoveOrderingEnPassantMoveBias = 77989;

        [SpsaMinValue("50000"), SpsaMaxValue("200000")]
        public static int MoveOrderingWinningCaptureBias = 142295;

        [SpsaMinValue("10000"), SpsaMaxValue("100000")]
        public static int MoveOrderingLosingCaptureBias = 15778;

        [SpsaMinValue("30000"), SpsaMaxValue("100000")]
        public static int MoveOrderingPromoteBias = 45184;

        [SpsaMinValue("30000"), SpsaMaxValue("100000")]
        public static int MoveOrderingCapturePromoteBias = 30512;

        [SpsaMinValue("10000"), SpsaMaxValue("100000")]
        public static int MoveOrderingKillerABias = 61238;

        [SpsaMinValue("10000"), SpsaMaxValue("100000")]
        public static int MoveOrderingCounterMoveBias = 87526;

        [SpsaMinValue("10000"), SpsaMaxValue("100000")]
        public static int InterestingNegaMaxMoveScore = 40731;

        [SpsaMinValue("10000"), SpsaMaxValue("100000")]
        public static int InterestingQuiescenceMoveScore = 40988;

        [SpsaMinValue("200"), SpsaMaxValue("300")]
        public static int ProbCutBetaMargin = 220;

        [SpsaMinValue("0"), SpsaMaxValue("6")]
        public static int ProbCutMinDepth = 3;

        [SpsaMinValue("30"), SpsaMaxValue("100")]
        public static int AsperationWindowA = 38;

        [SpsaMinValue("50"), SpsaMaxValue("200")]
        public static int AsperationWindowB = 60;

        [SpsaMinValue("100"), SpsaMaxValue("500")]
        public static int AsperationWindowC = 275;

        [SpsaMinValue("400"), SpsaMaxValue("1500")]
        public static int AsperationWindowD = 837;

        [SpsaMinValue("1400"), SpsaMaxValue("3000")]
        public static int AsperationWindowE = 2662;
#else
        public const int ReverseFutilityPruningMargin = 68;
        public const int ReverseFutilityPruningDepth = 7;
        public const int NullMovePruningDepth = 2;
        public const float NullMovePruningReductionA = 3;
        public const float NullMovePruningReductionB = 4;
        public const float NullMovePruningReductionC = 3;
        public const int RazorMarginA = 57;
        public const int RazorMarginB = 370;
        public const int InternalIterativeDeepeningDepth = 2;
        public const int LateMovePruningConstant = 8;
        public const int LateMoveReductionMinDepth = 3;
        public const int LateMoveReductionMinMoves = 2;
        public const float LateMoveReductionInterestingA = 0.188786f;
        public const float LateMoveReductionInterestingB = 2.4956496f;
        public const float LateMoveReductionA = 1.315961f;
        public const float LateMoveReductionB = 2.7831153f;
        public const int HistoryHeuristicMaxHistory = 9599;
        public const int HistoryHeuristicBonusMax = 425;
        public const int HistoryHeuristicBonusCoeff = 86;
        public const int MoveOrderingBestMoveBias = 240615;
        public const int MoveOrderingEnPassantMoveBias = 77989;
        public const int MoveOrderingWinningCaptureBias = 142295;
        public const int MoveOrderingLosingCaptureBias = 15778;
        public const int MoveOrderingPromoteBias = 45184;
        public const int MoveOrderingCapturePromoteBias = 30512;
        public const int MoveOrderingKillerABias = 61238;
        public const int MoveOrderingCounterMoveBias = 87526;
        public const int InterestingNegaMaxMoveScore = 40731;
        public const int InterestingQuiescenceMoveScore = 40988;
        public const int ProbCutBetaMargin = 220;
        public const int ProbCutMinDepth = 3;
        public const int AsperationWindowA = 38;
        public const int AsperationWindowB = 60;
        public const int AsperationWindowC = 275;
        public const int AsperationWindowD = 837;
        public const int AsperationWindowE = 2662;
#endif

        public static int* LateMovePruningInterestingReductionTable;
        public static int* LateMovePruningReductionTable;
        public static int* NullMovePruningReductionTable;
        static SpsaOptions()
        {
            NullMovePruningReductionTable = MemoryHelpers.Allocate<int>(Constants.MaxSearchDepth);
            LateMovePruningReductionTable = MemoryHelpers.Allocate<int>(Constants.MaxSearchDepth * 218);
            LateMovePruningInterestingReductionTable = MemoryHelpers.Allocate<int>(Constants.MaxSearchDepth * 218);
            UpdateNullMovePruningReductionTable();
            UpdateLateMovePruningReductionTable();
        }

        public static void UpdateNullMovePruningReductionTable()
        {
            for (var i = 0; i < Constants.MaxSearchDepth; i++)
            {
                NullMovePruningReductionTable[i] = (int)Math.Round(Math.Max(0, ((i - NullMovePruningReductionA) / NullMovePruningReductionB) + NullMovePruningReductionC));
            }
        }

        public static void UpdateLateMovePruningReductionTable()
        {
            for (var i = 0; i < Constants.MaxSearchDepth; i++)
            {
                for (var j = 0; j < 218; j++)
                {
                    LateMovePruningInterestingReductionTable[i*218 +j] = (int)Math.Round(LateMoveReductionInterestingA + (Math.Log(i) * Math.Log(j)) / LateMoveReductionInterestingB);
                    LateMovePruningReductionTable[i * 218 + j] = (int)Math.Round(LateMoveReductionA + (Math.Log(i) * Math.Log(j)) / LateMoveReductionB);
                }
            }
        }
    }

    public static class SpsaTuner
    {
        public static Dictionary<string, SpsaParameter> TuningParameters = new();

        static SpsaTuner()
        {
            ProcessUCIOptions();
        }

        public static string FormatParameter(SpsaParameter parameter)
        {
            const double minStepSize = 0.01;
            const double normalLearningRate = 0.02;

            if (parameter.Type == "int")
            {
                var min = int.Parse(parameter.MinValue);
                var max = int.Parse(parameter.MaxValue);
                var ss = Math.Max(minStepSize, (max - min) / 40.0);
                var lr = double.Round(Math.Max(normalLearningRate, normalLearningRate * (0.50 / ss)), 4);

                return
                    $"{parameter.Name}, {parameter.Type}, {parameter.DefaultValue}, {parameter.MinValue}, {parameter.MaxValue}, {ss}, {lr}";
            }
            else
            {
                var min = double.Parse(parameter.MinValue);
                var max = double.Parse(parameter.MaxValue);
                var ss = Math.Max(minStepSize, (max - min) / 40.0);
                var lr = double.Round(Math.Max(normalLearningRate, normalLearningRate * (0.50 / ss)), 4);

                return
                    $"{parameter.Name}, {parameter.Type}, {parameter.DefaultValue}, {parameter.MinValue}, {parameter.MaxValue}, {ss}, {lr}";
            }
        }

        public static void ProcessUCIOptions()
        {
            TuningParameters = new Dictionary<string, SpsaParameter>();

            foreach (var field in typeof(SpsaOptions).GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(x => !x.IsLiteral).ToList())
            {

                if (field.FieldType != typeof(int) && field.FieldType != typeof(float))
                {
                    continue;
                }
                var defaultValue = field.GetValue(null)?.ToString() ?? "0";
                // Retrieve the custom attributes, if present
                var minValueAttribute = field.GetCustomAttribute<SpsaMinValueAttribute>();
                var maxValueAttribute = field.GetCustomAttribute<SpsaMaxValueAttribute>();
                var ignoreAttribute = field.GetCustomAttribute<SpsaIgnoreAttribute>();
                if (ignoreAttribute != null)
                {
                    continue;
                }

                // Set MinValue and MaxValue using the attributes, or default to the default value
                var minValue = minValueAttribute?.MinValue ?? defaultValue;
                var maxValue = maxValueAttribute?.MaxValue ?? defaultValue;

                TuningParameters[field.Name.ToLower()] = new SpsaParameter()
                {
                    Name = field.Name,
                    DefaultValue = defaultValue,
                    Type = field.FieldType.Name == nameof(Int32) ? "int" : "float",
                    MinValue = minValue,
                    MaxValue = maxValue,
                    FieldHandle = field
                };
            }
        }

        public static List<string> PrintSPSAParams()
        {
            var parameters = new List<string>();
            foreach (var parameter in TuningParameters.Values)
            {
                parameters.Add(FormatParameter(parameter));
            }

            return parameters;
        }

        public static void SetParameterValue(string[] tokens)
        {
            var parameter = TuningParameters[tokens[2].ToLower()];
            if (parameter.Type == "int")
            {
                if (tokens[3] == "value" && int.TryParse(tokens[4], out var value))
                {
                    parameter.FieldHandle.SetValue(null, value);
                }
            }
            else
            {
                if (tokens[3] == "value" && float.TryParse(tokens[4], out var value))
                {
                    parameter.FieldHandle.SetValue(null, value);
                }
            }

            if (parameter.Name is "HistoryHeuristicBonusMax" or "HistoryHeuristicBonusCoeff")
            {
                HistoryHeuristicExtensions.UpdateBonusTable();
            }else if (parameter.Name.Contains("NullMovePruningReduction"))
            {
                SpsaOptions.UpdateNullMovePruningReductionTable();
            }else if (parameter.Name.Contains("LateMoveReduction"))
            {
                SpsaOptions.UpdateLateMovePruningReductionTable();
            }

            ProcessUCIOptions();
        }

        public static bool HasParameter(string parameterName)
        {
            return TuningParameters.ContainsKey(parameterName);
        }
    }

}