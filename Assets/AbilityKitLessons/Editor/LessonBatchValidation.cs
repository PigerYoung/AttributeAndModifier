using System;
using UnityEditor;
using UnityEngine;

namespace AttributeAndModifier.Lessons.Editor
{
    /// <summary>
    /// 编辑器批处理入口：执行全部课程，验证课程之间没有隐藏的场景依赖。
    /// </summary>
    public static class LessonBatchValidation
    {
        public static void Run()
        {
            LessonCatalog.EnsureInitialized();
            LessonCatalog.Freeze();

            var lessons = new ILessonExample[]
            {
                new Lesson01_BasicAttribute(),
                new Lesson02_Operators(),
                new Lesson03_Lifetime(),
                new Lesson04_MagnitudeSources(),
                new Lesson05_Dependencies(),
                new Lesson06_EffectAndConstraint(),
                new Lesson07_Stacking(),
                new Lesson08_Extensions(),
                new Lesson09_ExplainAndCache()
            };

            foreach (var lesson in lessons)
            {
                var output = lesson.Run();
                if (string.IsNullOrEmpty(output)) throw new InvalidOperationException(lesson.Title + " returned empty output.");
                Debug.Log("[AbilityKit Lesson] " + lesson.Title + Environment.NewLine + output);
            }

            Debug.Log("[AbilityKit Lesson] All lessons passed.");
            EditorApplication.Exit(0);
        }
    }
}
