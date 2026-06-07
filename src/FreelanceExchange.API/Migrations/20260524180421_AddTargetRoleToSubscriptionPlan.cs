using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceExchange.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetRoleToSubscriptionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetRole",
                table: "SubscriptionPlans",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetRole",
                table: "SubscriptionPlans");
        }
    }
}
