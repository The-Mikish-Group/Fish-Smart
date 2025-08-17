using Members.Data;
using Members.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Members.Data
{
    public static class ColorVarSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context, string cssFilePath)
        {
            var cssContent = await System.IO.File.ReadAllTextAsync(cssFilePath);
            var regex = new Regex(@"(--(?<name>[\w-]+))\s*,\s*(?<value>#[0-9a-fA-F]{3,6})\)");
            var matches = regex.Matches(cssContent);

            Console.WriteLine($"Found {matches.Count} matches in css file.");
            
            // PERFORMANCE FIX: Get all existing color names in one query instead of 45 individual queries
            var existingColorNames = await context.ColorVars
                .Select(c => c.Name)
                .ToListAsync();
            
            var colorsToAdd = new List<ColorVar>();
            
            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;
                var value = match.Groups["value"].Value;

                // Check against the in-memory list instead of database
                if (!existingColorNames.Contains(name))
                {
                    Console.WriteLine($"Adding color {name} with value {value}");
                    colorsToAdd.Add(new ColorVar { Name = name, Value = value });
                }
            }

            // Batch insert all new colors at once
            if (colorsToAdd.Any())
            {
                context.ColorVars.AddRange(colorsToAdd);
                await context.SaveChangesAsync();
            }
        }
    }
}
