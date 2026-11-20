using AbilityKit.Modifiers;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// [AK-INVALIDATE] 从一条修饰器收集失效依赖。
    /// 只读序列化数据，不调用 SingleModifierData.Modify 或 CreateModifier，避免分配。
    /// 结果分两类：修饰器引用的源属性 RawId，以及修饰器读取的上下文输入键。
    /// </summary>
    internal struct ModifierDependencyNotes
    {
        private const int MaxNotes = 4;

        public int SourceCount;
        public int Source0;
        public int Source1;
        public int Source2;
        public int Source3;

        public int InputKeyCount;
        public string InputKey0;
        public string InputKey1;
        public string InputKey2;
        public string InputKey3;

        public int GetSource(int index)
        {
            switch (index)
            {
                case 0: return Source0;
                case 1: return Source1;
                case 2: return Source2;
                case 3: return Source3;
                default: return 0;
            }
        }

        public string GetInputKey(int index)
        {
            switch (index)
            {
                case 0: return InputKey0;
                case 1: return InputKey1;
                case 2: return InputKey2;
                case 3: return InputKey3;
                default: return null;
            }
        }

        public static ModifierDependencyNotes Collect(in ModifierData modifier)
        {
            var notes = default(ModifierDependencyNotes);
            CollectMagnitude(modifier.Magnitude, ref notes);
            return notes;
        }

        private static void CollectMagnitude(in MagnitudeSource magnitude, ref ModifierDependencyNotes notes)
        {
            switch (magnitude.Type)
            {
                case MagnitudeSourceType.Scalable:
                    AddInputKey(ref notes, "level");
                    break;

                case MagnitudeSourceType.Attribute:
                    AddSource(ref notes, (int)magnitude.AttributeKey.Packed);
                    break;

                case MagnitudeSourceType.TimeDecay:
                    AddInputKey(ref notes, "elapsedTime");
                    break;

                case MagnitudeSourceType.ContextFloat:
                    AddInputKey(ref notes, magnitude.ContextFloatKey);
                    break;

                case MagnitudeSourceType.Pipeline:
                    CollectPipeline(magnitude.PipelineData, ref notes);
                    break;
            }
        }

        private static void CollectPipeline(in MagnitudePipelineData pipeline, ref ModifierDependencyNotes notes)
        {
            int count = pipeline.Count;
            for (int i = 0; i < count && i < MaxNotes; i++)
            {
                switch (i)
                {
                    case 0: CollectStep(pipeline.Modifier0, ref notes); break;
                    case 1: CollectStep(pipeline.Modifier1, ref notes); break;
                    case 2: CollectStep(pipeline.Modifier2, ref notes); break;
                    case 3: CollectStep(pipeline.Modifier3, ref notes); break;
                }
            }
        }

        private static void CollectStep(in SingleModifierData step, ref ModifierDependencyNotes notes)
        {
            switch (step.TypeId)
            {
                case 1: // TimeDecay
                    AddInputKey(ref notes, "elapsedTime");
                    break;

                case 2: // LevelCurve
                    AddInputKey(ref notes, "level");
                    break;

                case 3: // AttributeRef
                    AddSource(ref notes, (int)step.Param0);
                    break;
            }
        }

        private static void AddSource(ref ModifierDependencyNotes notes, int sourceRawId)
        {
            if (sourceRawId == 0) return;
            if (notes.SourceCount >= MaxNotes) return;

            switch (notes.SourceCount)
            {
                case 0: notes.Source0 = sourceRawId; break;
                case 1: notes.Source1 = sourceRawId; break;
                case 2: notes.Source2 = sourceRawId; break;
                case 3: notes.Source3 = sourceRawId; break;
            }

            notes.SourceCount++;
        }

        private static void AddInputKey(ref ModifierDependencyNotes notes, string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (notes.InputKeyCount >= MaxNotes) return;

            switch (notes.InputKeyCount)
            {
                case 0: notes.InputKey0 = key; break;
                case 1: notes.InputKey1 = key; break;
                case 2: notes.InputKey2 = key; break;
                case 3: notes.InputKey3 = key; break;
            }

            notes.InputKeyCount++;
        }
    }
}
