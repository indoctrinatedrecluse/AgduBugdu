using System;
using Avalonia.Input;
using Xunit;

namespace AgduBugdu.Tests;

public class KeyGestureTests
{
    [Fact]
    public void TestKeyGestureParsing()
    {
        var kg1 = KeyGesture.Parse("Ctrl+OemTilde");
        Assert.NotNull(kg1);
        Assert.Equal(Key.OemTilde, kg1.Key);
    }
}
