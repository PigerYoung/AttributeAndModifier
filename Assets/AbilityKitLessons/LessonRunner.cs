using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 教学运行器：把每个课程显示成按钮。
    /// 使用方式：把本脚本挂到场景中的任意空 GameObject，点击 Play 即可开始。
    /// </summary>
    public sealed class LessonRunner : MonoBehaviour
    {
        private readonly List<ILessonExample> _lessons = new List<ILessonExample>
        {
            new Lesson01_BasicAttribute(),
            new Lesson02_Operators(),
            new Lesson03_Lifetime(),
            new Lesson04_MagnitudeSources(),
            new Lesson05_Dependencies(),
            new Lesson06_EffectAndConstraint(),
            new Lesson07_Stacking(),
            new Lesson08_Extensions(),
            new Lesson09_ExplainAndCache(),
            new Lesson10_ComposerStrategy(),
            new Lesson11_ModifierComposer(),
            new Lesson12_RegistryAndContexts(),
            new Lesson13_CustomFormulaAndConstraint(),
            new Lesson14_SnapshotVsLive()
        };

        private Vector2 _scroll;
        private string _output = "请选择一个课程。";
        private string _purpose = "每个课程只表达一个主要设计点。建议按 01 → 14 顺序运行。";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRunnerIfNeeded()
        {
            if (FindFirstObjectByType<LessonRunner>() != null) return;
            var runnerObject = new GameObject("AbilityKit Lesson Runner");
            runnerObject.AddComponent<LessonRunner>();
        }

        private void Awake()
        {
            LessonCatalog.EnsureInitialized();
            LessonCatalog.Freeze();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20f, 20f, Mathf.Min(Screen.width - 40f, 760f), Screen.height - 40f), GUI.skin.box);
            GUILayout.Label("AbilityKit Attribute / Modifier 学习实验室", GUI.skin.大标题());
            GUILayout.Label("目标：通过可运行的小实验理解框架，而不是直接阅读完整 MOBA 工程。");
            GUILayout.Space(8f);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(230f));
            for (var i = 0; i < _lessons.Count; i++)
            {
                var lesson = _lessons[i];
                if (GUILayout.Button(lesson.Title, GUILayout.Height(30f)))
                {
                    _purpose = lesson.Purpose;
                    try
                    {
                        _output = lesson.Run();
                    }
                    catch (Exception exception)
                    {
                        _output = $"运行失败: {exception}";
                        Debug.LogException(exception, this);
                    }
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label("本例主要表达：" + _purpose);
            GUILayout.Label("运行结果：");
            GUILayout.TextArea(_output, GUILayout.ExpandHeight(true));
            GUILayout.EndArea();
        }
    }

    internal static class LessonGuiExtensions
    {
        public static GUIStyle 大标题(this GUISkin skin)
        {
            var style = new GUIStyle(skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            return style;
        }
    }
}
