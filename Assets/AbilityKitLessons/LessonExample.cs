using System;

namespace AttributeAndModifier.Lessons
{
    /// <summary>
    /// 一个课程的统一输出接口。每个示例只表达一个主要设计点。
    /// </summary>
    public interface ILessonExample
    {
        string Title { get; }
        string Purpose { get; }
        string Run();
    }

    public abstract class LessonExampleBase : ILessonExample
    {
        public abstract string Title { get; }
        public abstract string Purpose { get; }
        public abstract string Run();

        protected static string Line(string text) => text + Environment.NewLine;
    }
}
