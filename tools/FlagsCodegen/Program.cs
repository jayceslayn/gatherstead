using System.Text;
using Gatherstead.Data.Entities;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: FlagsCodegen <output-path>");
    return 1;
}

var sb = new StringBuilder();
sb.AppendLine("// Auto-generated from C# flag enums — do not edit.");
sb.AppendLine("// Run scripts/generate-openapi.sh to regenerate.");
sb.AppendLine();

sb.AppendLine("export const MEAL_TYPE_FLAGS = {");
foreach (var name in Enum.GetNames<MealTypeFlags>())
    sb.AppendLine($"  {name}: 0x{(int)(object)Enum.Parse<MealTypeFlags>(name):X2},");
sb.AppendLine("} as const");
sb.AppendLine();

sb.AppendLine("export const TASK_SLOT_FLAGS = {");
foreach (var name in Enum.GetNames<TaskTimeSlotFlags>())
    sb.AppendLine($"  {name}: 0x{(int)(object)Enum.Parse<TaskTimeSlotFlags>(name):X2},");
sb.AppendLine("} as const");

await File.WriteAllTextAsync(args[0], sb.ToString());
Console.WriteLine($"Written: {args[0]}");
return 0;
