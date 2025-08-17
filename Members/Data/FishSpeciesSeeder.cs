using Members.Models;
using Microsoft.EntityFrameworkCore;

namespace Members.Data
{
    public static class FishSpeciesSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Check if fish species already exist
            if (await context.FishSpecies.AnyAsync())
            {
                return; // Database has been seeded
            }

            var fishSpecies = new List<FishSpecies>
            {
                // FRESHWATER SPECIES - Popular Game Fish
                new FishSpecies
                {
                    CommonName = "Largemouth Bass",
                    ScientificName = "Micropterus salmoides",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 12,
                    MaxSize = 25,
                    SeasonStart = 3,
                    SeasonEnd = 11,
                    RegulationNotes = "Check local regulations for size and bag limits",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Smallmouth Bass",
                    ScientificName = "Micropterus dolomieu",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 8,
                    MaxSize = 20,
                    SeasonStart = 3,
                    SeasonEnd = 11,
                    RegulationNotes = "Popular sport fish, check size limits",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Northern Pike",
                    ScientificName = "Esox lucius",
                    WaterType = "Fresh",
                    Region = "Northern regions",
                    MinSize = 18,
                    MaxSize = 40,
                    SeasonStart = 5,
                    SeasonEnd = 3,
                    RegulationNotes = "Season may span winter months",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Walleye",
                    ScientificName = "Sander vitreus",
                    WaterType = "Fresh",
                    Region = "Great Lakes region",
                    MinSize = 12,
                    MaxSize = 20,
                    SeasonStart = 4,
                    SeasonEnd = 11,
                    RegulationNotes = "Excellent eating, check bag limits",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Muskellunge",
                    ScientificName = "Esox masquinongy",
                    WaterType = "Fresh",
                    Region = "Great Lakes region",
                    MinSize = 30,
                    MaxSize = 50,
                    SeasonStart = 6,
                    SeasonEnd = 11,
                    RegulationNotes = "Catch and release recommended",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Chain Pickerel",
                    ScientificName = "Esox niger",
                    WaterType = "Fresh",
                    Region = "Eastern US",
                    MinSize = 10,
                    MaxSize = 20,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Year-round season in many areas",
                    IsActive = true
                },

                // FRESHWATER SPECIES - Panfish
                new FishSpecies
                {
                    CommonName = "Bluegill",
                    ScientificName = "Lepomis macrochirus",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 4,
                    MaxSize = 10,
                    SeasonStart = 5,
                    SeasonEnd = 9,
                    RegulationNotes = "Great for beginners, year-round in many areas",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Black Crappie",
                    ScientificName = "Pomoxis nigromaculatus",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 6,
                    MaxSize = 12,
                    SeasonStart = 3,
                    SeasonEnd = 5,
                    RegulationNotes = "Best during spring spawn",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "White Crappie",
                    ScientificName = "Pomoxis annularis",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 6,
                    MaxSize = 12,
                    SeasonStart = 3,
                    SeasonEnd = 5,
                    RegulationNotes = "Similar to black crappie",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Redear Sunfish",
                    ScientificName = "Lepomis microlophus",
                    WaterType = "Fresh",
                    Region = "Southern US",
                    MinSize = 4,
                    MaxSize = 8,
                    SeasonStart = 4,
                    SeasonEnd = 9,
                    RegulationNotes = "Also called shellcracker",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Rock Bass",
                    ScientificName = "Ambloplites rupestris",
                    WaterType = "Fresh",
                    Region = "Eastern North America",
                    MinSize = 4,
                    MaxSize = 8,
                    SeasonStart = 5,
                    SeasonEnd = 9,
                    RegulationNotes = "Year-round season",
                    IsActive = true
                },

                // FRESHWATER SPECIES - Catfish
                new FishSpecies
                {
                    CommonName = "Channel Catfish",
                    ScientificName = "Ictalurus punctatus",
                    WaterType = "Fresh",
                    Region = "North America",
                    MinSize = 12,
                    MaxSize = 30,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Year-round fishing",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Blue Catfish",
                    ScientificName = "Ictalurus furcatus",
                    WaterType = "Fresh",
                    Region = "Southern US",
                    MinSize = 20,
                    MaxSize = 40,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Large specimens, year-round",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Flathead Catfish",
                    ScientificName = "Pylodictis olivaris",
                    WaterType = "Fresh",
                    Region = "Central US",
                    MinSize = 15,
                    MaxSize = 40,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Trophy fish, year-round",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "White Catfish",
                    ScientificName = "Ameiurus catus",
                    WaterType = "Fresh",
                    Region = "Eastern US",
                    MinSize = 8,
                    MaxSize = 18,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Year-round fishing",
                    IsActive = true
                },

                // FRESHWATER SPECIES - Trout
                new FishSpecies
                {
                    CommonName = "Rainbow Trout",
                    ScientificName = "Oncorhynchus mykiss",
                    WaterType = "Fresh",
                    Region = "Widely stocked",
                    MinSize = 8,
                    MaxSize = 16,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Check trout stamp requirements",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Brown Trout",
                    ScientificName = "Salmo trutta",
                    WaterType = "Fresh",
                    Region = "Cool waters",
                    MinSize = 8,
                    MaxSize = 20,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Often catch and release",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Brook Trout",
                    ScientificName = "Salvelinus fontinalis",
                    WaterType = "Fresh",
                    Region = "Eastern mountains",
                    MinSize = 6,
                    MaxSize = 14,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Native species protection",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Lake Trout",
                    ScientificName = "Salvelinus namaycush",
                    WaterType = "Fresh",
                    Region = "Great Lakes",
                    MinSize = 15,
                    MaxSize = 30,
                    SeasonStart = 5,
                    SeasonEnd = 9,
                    RegulationNotes = "Deep water fishing",
                    IsActive = true
                },

                // FRESHWATER SPECIES - Other Popular
                new FishSpecies
                {
                    CommonName = "White Bass",
                    ScientificName = "Morone chrysops",
                    WaterType = "Fresh",
                    Region = "Central US",
                    MinSize = 8,
                    MaxSize = 15,
                    SeasonStart = 4,
                    SeasonEnd = 6,
                    RegulationNotes = "School fish, spring runs",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Yellow Perch",
                    ScientificName = "Perca flavescens",
                    WaterType = "Fresh",
                    Region = "Northern US",
                    MinSize = 6,
                    MaxSize = 12,
                    SeasonStart = 4,
                    SeasonEnd = 11,
                    RegulationNotes = "Excellent eating",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Sauger",
                    ScientificName = "Sander canadensis",
                    WaterType = "Fresh",
                    Region = "Great Lakes region",
                    MinSize = 8,
                    MaxSize = 15,
                    SeasonStart = 4,
                    SeasonEnd = 11,
                    RegulationNotes = "Similar to walleye",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Common Carp",
                    ScientificName = "Cyprinus carpio",
                    WaterType = "Fresh",
                    Region = "Widespread",
                    MinSize = 12,
                    MaxSize = 30,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Year-round, no limits typically",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Longnose Gar",
                    ScientificName = "Lepisosteus osseus",
                    WaterType = "Fresh",
                    Region = "Central and Eastern US",
                    MinSize = 18,
                    MaxSize = 36,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Primitive species, year-round",
                    IsActive = true
                },

                // SALTWATER/BOTH SPECIES
                new FishSpecies
                {
                    CommonName = "Striped Bass",
                    ScientificName = "Morone saxatilis",
                    WaterType = "Both",
                    Region = "Atlantic and Pacific coasts",
                    MinSize = 18,
                    MaxSize = 35,
                    SeasonStart = 4,
                    SeasonEnd = 11,
                    RegulationNotes = "Size and bag limits vary by state",
                    IsActive = true
                },

                // SALTWATER SPECIES - Inshore/Nearshore
                new FishSpecies
                {
                    CommonName = "Red Snapper",
                    ScientificName = "Lutjanus campechanus",
                    WaterType = "Salt",
                    Region = "Gulf of Mexico",
                    MinSize = 12,
                    MaxSize = 25,
                    SeasonStart = 6,
                    SeasonEnd = 7,
                    RegulationNotes = "Highly regulated, limited season",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Red Grouper",
                    ScientificName = "Epinephelus morio",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 15,
                    MaxSize = 40,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Size limits enforced",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Summer Flounder",
                    ScientificName = "Paralichthys dentatus",
                    WaterType = "Salt",
                    Region = "Atlantic coast",
                    MinSize = 10,
                    MaxSize = 20,
                    SeasonStart = 5,
                    SeasonEnd = 10,
                    RegulationNotes = "Flatfish, size limits vary",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Redfish",
                    ScientificName = "Sciaenops ocellatus",
                    WaterType = "Salt",
                    Region = "Gulf and Atlantic coasts",
                    MinSize = 18,
                    MaxSize = 30,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Slot limits in many areas",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Speckled Trout",
                    ScientificName = "Cynoscion nebulosus",
                    WaterType = "Salt",
                    Region = "Gulf and Atlantic coasts",
                    MinSize = 12,
                    MaxSize = 20,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Popular inshore species",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Black Drum",
                    ScientificName = "Pogonias cromis",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf coasts",
                    MinSize = 12,
                    MaxSize = 30,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Year-round fishing",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Sheepshead",
                    ScientificName = "Archosargus probatocephalus",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf coasts",
                    MinSize = 8,
                    MaxSize = 16,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Structure fish, year-round",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Tarpon",
                    ScientificName = "Megalops atlanticus",
                    WaterType = "Salt",
                    Region = "Gulf and Atlantic coasts",
                    MinSize = 36,
                    MaxSize = 80,
                    SeasonStart = 5,
                    SeasonEnd = 9,
                    RegulationNotes = "Catch and release only",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Snook",
                    ScientificName = "Centropomus undecimalis",
                    WaterType = "Salt",
                    Region = "Florida",
                    MinSize = 18,
                    MaxSize = 35,
                    SeasonStart = 9,
                    SeasonEnd = 11,
                    RegulationNotes = "Closed season protection",
                    IsActive = true
                },

                // SALTWATER SPECIES - Offshore
                new FishSpecies
                {
                    CommonName = "King Mackerel",
                    ScientificName = "Scomberomorus cavalla",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 20,
                    MaxSize = 40,
                    SeasonStart = 3,
                    SeasonEnd = 11,
                    RegulationNotes = "Seasonal migrations",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Spanish Mackerel",
                    ScientificName = "Scomberomorus maculatus",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 10,
                    MaxSize = 20,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Smaller than king mackerel",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Cobia",
                    ScientificName = "Rachycentron canadum",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 25,
                    MaxSize = 50,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Excellent eating",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Mahi Mahi",
                    ScientificName = "Coryphaena hippurus",
                    WaterType = "Salt",
                    Region = "Offshore Atlantic and Gulf",
                    MinSize = 20,
                    MaxSize = 40,
                    SeasonStart = 4,
                    SeasonEnd = 10,
                    RegulationNotes = "Pelagic species",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Yellowfin Tuna",
                    ScientificName = "Thunnus albacares",
                    WaterType = "Salt",
                    Region = "Offshore Atlantic and Gulf",
                    MinSize = 25,
                    MaxSize = 60,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Deep water fishing",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Blackfin Tuna",
                    ScientificName = "Thunnus atlanticus",
                    WaterType = "Salt",
                    Region = "Atlantic",
                    MinSize = 15,
                    MaxSize = 25,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Smaller tuna species",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Wahoo",
                    ScientificName = "Acanthocybium solandri",
                    WaterType = "Salt",
                    Region = "Offshore Atlantic and Gulf",
                    MinSize = 30,
                    MaxSize = 60,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Fast pelagic species",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Atlantic Sailfish",
                    ScientificName = "Istiophorus platypterus",
                    WaterType = "Salt",
                    Region = "Atlantic",
                    MinSize = 60,
                    MaxSize = 100,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Catch and release billfish",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Blue Marlin",
                    ScientificName = "Makaira nigricans",
                    WaterType = "Salt",
                    Region = "Atlantic",
                    MinSize = 80,
                    MaxSize = 150,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Catch and release billfish",
                    IsActive = true
                },

                // SALTWATER SPECIES - Sharks & Rays
                new FishSpecies
                {
                    CommonName = "Blacktip Shark",
                    ScientificName = "Carcharhinus limbatus",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 24,
                    MaxSize = 60,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Shark regulations apply",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Bull Shark",
                    ScientificName = "Carcharhinus leucas",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 36,
                    MaxSize = 96,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Large predator species",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Hammerhead Shark",
                    ScientificName = "Sphyrna lewini",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 36,
                    MaxSize = 120,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Protected species in some areas",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Southern Stingray",
                    ScientificName = "Dasyatis americana",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 12,
                    MaxSize = 60,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Handle with care",
                    IsActive = true
                },

                // SALTWATER SPECIES - Bottom Fish
                new FishSpecies
                {
                    CommonName = "Vermillion Snapper",
                    ScientificName = "Rhomboplites aurorubens",
                    WaterType = "Salt",
                    Region = "Gulf and Atlantic",
                    MinSize = 8,
                    MaxSize = 16,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Deep water bottom fish",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Gray Triggerfish",
                    ScientificName = "Balistes capriscus",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 8,
                    MaxSize = 15,
                    SeasonStart = 1,
                    SeasonEnd = 12,
                    RegulationNotes = "Regulated in federal waters",
                    IsActive = true
                },
                new FishSpecies
                {
                    CommonName = "Greater Amberjack",
                    ScientificName = "Seriola dumerili",
                    WaterType = "Salt",
                    Region = "Atlantic and Gulf",
                    MinSize = 20,
                    MaxSize = 50,
                    SeasonStart = 5,
                    SeasonEnd = 7,
                    RegulationNotes = "Closed season during spawn",
                    IsActive = true
                }
            };

            context.FishSpecies.AddRange(fishSpecies);
            await context.SaveChangesAsync();
        }
    }
}