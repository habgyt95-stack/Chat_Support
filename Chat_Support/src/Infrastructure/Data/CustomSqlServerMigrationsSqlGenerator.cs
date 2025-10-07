using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Update;

namespace Chat_Support.Infrastructure.Data;

public class CustomSqlServerMigrationsSqlGenerator : SqlServerMigrationsSqlGenerator
{
    public CustomSqlServerMigrationsSqlGenerator(
        MigrationsSqlGeneratorDependencies dependencies,
        ICommandBatchPreparer commandBatchPreparer)
        : base(dependencies, commandBatchPreparer)
    {
    }

    public override IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model = null,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var builder = new MigrationCommandListBuilder(Dependencies);

        foreach (var operation in operations)
        {
            if (operation is CreateTableOperation createTableOperation)
            {
                // شروع یک batch جدید
                builder.EndCommand(suppressTransaction: true);

                builder
                    .Append("IF OBJECT_ID(N'")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(createTableOperation.Name, createTableOperation.Schema))
                    .Append("', 'U') IS NULL")
                    .AppendLine()
                    .AppendLine("BEGIN")
                    .AppendLine();

                using (builder.Indent())
                {
                    var commands = base.Generate(new[] { operation }, model, options);
                    foreach (var command in commands)
                    {
                        // حذف ; از انتهای دستور CREATE TABLE اگر وجود داشته باشد
                        var commandText = command.CommandText.TrimEnd();
                        if (commandText.EndsWith(";"))
                            commandText = commandText.Substring(0, commandText.Length - 1);
                            
                        builder.Append(commandText);
                        
                        // فقط برای دستورات داخلی، EndCommand را بدون suppressTransaction صدا می‌زنیم
                        if (command != commands[commands.Count - 1])
                            builder.EndCommand();
                    }
                }

                builder
                    .AppendLine()
                    .Append("END")  // بدون ;
                    .AppendLine();

                // پایان batch با suppressTransaction = true
                builder.EndCommand(suppressTransaction: true);
            }
            else
            {
                var commands = base.Generate(new[] { operation }, model, options);
                foreach (var command in commands)
                {
                    builder.Append(command.CommandText);
                    builder.EndCommand();
                }
            }
        }

        return builder.GetCommandList();
    }
}
