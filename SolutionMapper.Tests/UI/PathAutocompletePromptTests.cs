using SolutionMapper.UI;

namespace SolutionMapper.Tests.UI;

public class PathAutocompletePromptTests
{
    [Fact]
    public void Prompt_exposes_empty_initial_value()
    {
        var method = typeof(PathAutocompletePrompt).GetMethod(nameof(PathAutocompletePrompt.Prompt));

        var initial = Assert.Single(method!.GetParameters(), parameter => parameter.Name == "initial");
        Assert.True(initial.HasDefaultValue);
        Assert.Equal("", initial.DefaultValue);
    }
}
