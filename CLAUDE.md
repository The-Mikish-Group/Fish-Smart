# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is "Fish-Smart" - an ASP.NET Core .NET 9 MVC web application with Identity for the Oaks-Village.com HOA (Homeowners Association). The application features a comprehensive Accounts Receivable system with reporting capabilities, user management, document management, and image galleries.

## 🚀 RECENT MAJOR FEATURE: Premium Background Removal System (August 2024)

### Current Status: ✅ PRODUCTION READY 
**Premium background removal with multiple API services is fully implemented and working.**

#### What's Working:
- **3 Background Removal Methods Available:**
  - **Standard (Free)**: AI/ONNX models or color-based detection 
  - **Remove.bg ($0.50/image)**: Professional API with 5 free/month
  - **Clipdrop ($0.50/image)**: Stability AI API with free credits available

#### Recent Session Progress (Aug 18, 2024):
1. ✅ **Fixed UI/UX Issues**: Method selection now at top of modal, no scrolling needed
2. ✅ **Fixed Confusing Dialog**: Warning modal now shows correct service info based on selection
3. ✅ **Added Clipdrop Support**: Third premium service option for testing comparison 
4. ✅ **Backend Integration**: All 3 methods properly routed in ImageViewerController
5. ✅ **Quality Confirmed**: Premium services producing much better results than AI method

#### Key Implementation Details:
- **UI Location**: Method selection in `Views/Shared/_BackgroundSelectorModal.cshtml` at top of modal
- **API Integration**: `Controllers/ImageViewerController.cs` handles `removebg`, `clipdrop`, `ai` methods
- **Billing System**: Integrated with existing A/R system, auto-invoicing for overages
- **Service Classes**: `ProductionBackgroundRemovalService.cs` (Remove.bg), `ClipdropService.cs`
- **Database**: `BackgroundRemovalUsage` table tracks usage and billing

#### Next Session Tasks:
- **Continue Testing**: User has Remove.bg (5 free/month) + Clipdrop (100 free credits) to compare
- **Potential Refinements**: Monitor for any edge cases or user feedback
- **Performance Monitoring**: Check if billing/usage tracking working correctly

#### Important Notes:
- **User Status**: Has Premium role, can access all methods
- **API Keys**: Both Remove.bg and Clipdrop configured and working
- **Quality**: Premium services significantly better than Standard AI method
- **UI Flow**: Background button → Method selection at top → Select background → Confirmation dialog → Processing

## Build and Development Commands

### Building the Application
```bash
# Build the solution
dotnet build Fish-Smart.sln

# Build specific configuration
dotnet build Fish-Smart.sln --configuration Release
```

### Running the Application
```bash
# Run in development mode
cd Members
dotnet run

# Run with specific configuration
dotnet run --configuration Release
```

### Database Operations
```bash
# Add new migration
cd Members
dotnet ef migrations add [MigrationName]

# Update database
dotnet ef database update

# Drop database (development only)
dotnet ef database drop
```

### Testing
There are no specific test commands configured in this repository. Tests should be added as needed.

## Architecture Overview

### Project Structure
- **Single Project**: `Members/` - Contains the entire web application
- **Areas**: Three main functional areas:
  - `Admin/` - Administrative functions (accounts receivable, user management, reporting)
  - `Identity/` - User authentication and account management
  - `Information/` - Public information pages
  - `Member/` - Member-specific functionality

### Key Technologies and Dependencies
- **.NET 9** with ASP.NET Core MVC
- **Entity Framework Core** with SQL Server
- **ASP.NET Core Identity** for authentication/authorization
- **Syncfusion PDF** libraries for PDF generation
- **SixLabors.ImageSharp** for image processing
- **Bootstrap** and custom CSS for styling

### Database Architecture
The application uses Entity Framework with the following key entities:
- **Identity tables** (AspNetUsers, AspNetRoles, etc.)
- **UserProfile** - Extended user information
- **Invoices/Payments/UserCredits** - Accounts receivable system
- **BillableAssets** - Properties/assets that can be billed
- **PDFCategories/CategoryFiles** - Document management
- **AdminTasks/TaskInstances** - Task management system
- **ColorVars** - Dynamic color theming

### Authentication & Authorization
- Role-based system with three roles: Admin, Member, Manager
- Admin accounts auto-created from environment variables
- Email confirmation required for new accounts

## Environment Configuration

The application relies heavily on environment variables for sensitive configuration:

### Required Environment Variables
- `DB_SERVER_SMARTCATCH` - Database server
- `DB_USER_SMARTCATCH` - Database username  
- `DB_PASSWORD_SMARTCATCH` - Database password
- `DB_NAME_SMARTCATCH` - Database name
- `ADMIN_EMAIL_SMARTCATCH` - Default admin email
- `ADMIN_PASSWORD_SMARTCATCH` - Default admin password
- `SYNCFUSION_KEY` - Syncfusion license key
- `DEFAULT_CITY_SMARTCATCH` - Default city for admin user
- `DEFAULT_STATE_SMARTCATCH` - Default state for admin user
- `DEFAULT_ZIPCODE_SMARTCATCH` - Default zip code for admin user
- `DEFAULT_NAME_SMARTCATCH` - Default organization name

### Configuration Files
- `appsettings.json` - Base configuration (uses LocalDB by default)
- `appsettings.Development.json` - Development overrides
- Connection strings from environment variables override appsettings.json

## Key Features and Business Logic

### Accounts Receivable System
This is the core feature with sophisticated automation:
- **Automatic credit application** - Credits automatically applied to outstanding balances
- **Intelligent overpayment handling** - Overpayments credited and applied to other invoices
- **Batch invoice processing** - Create and review invoice batches before finalization
- **Late fee calculation** - Configurable percentage or minimum fee system
- **Comprehensive audit trail** - All transactions logged in CreditApplication table

### Document Management
- PDF category-based file organization
- Role-based access to protected files
- PDF generation using Syncfusion libraries

### Image Galleries
- Gallery management with automatic thumbnail generation
- Image upload and organization by gallery name
- Responsive gallery viewing

### Dynamic Color Theming
- CSS custom properties loaded from database (ColorVars table)
- Admin interface for color management
- Dynamic color injection via filters

## Development Guidelines

### Code Patterns
- **Page Models**: Razor Pages use code-behind PageModel classes
- **Services**: Business logic in service classes (EmailService, TaskManagementService)
- **Filters**: Global filters for dynamic color loading
- **View Components**: Reusable UI components (DynamicColorsViewComponent)

### Database Patterns
- **Soft deletes** where appropriate (BillableAssets with SetNull)
- **Audit trails** for financial transactions
- **Unique constraints** on business keys (PlotID, Task+Year+Month)
- **Proper foreign key relationships** with appropriate delete behaviors

### Security Considerations
- All sensitive data stored in environment variables
- Data protection keys persisted to database
- Antiforgery tokens with custom configuration
- Role-based authorization throughout

## File Organization

### Static Assets
- `wwwroot/css/` - Stylesheets including dynamic color CSS
- `wwwroot/js/` - JavaScript files
- `wwwroot/Galleries/` - Image gallery storage
- `ProtectedFiles/` - Secured document storage

### Views and Pages
- `Views/` - MVC views organized by controller
- `Areas/*/Pages/` - Razor Pages organized by functional area
- Shared layouts and partials in `Views/Shared/`

## Development Workflow

1. **Environment Setup**: Ensure all required environment variables are set
2. **Database**: Run migrations to set up/update database schema
3. **Development**: Use `dotnet run` for local development
4. **Testing**: Currently manual - automated tests should be added
5. **Deployment**: Uses publish profiles for various hosting environments

## Recent Updates and Fixes (2025-01-18)

### Database Field Padding Issue Resolution
- **Issue**: Database string fields (PhotoUrl, CoverImageUrl, ImageUrl) were using CHAR type causing massive trailing whitespace padding (~300 characters)
- **Root Cause**: CHAR fields pad with spaces to fill column width, causing file path resolution failures
- **Solution**: Changed to VARCHAR(500) in EF Designer for all image URL fields:
  - `Catch.PhotoUrl` 
  - `CatchAlbum.CoverImageUrl`
  - `Background.ImageUrl`
- **Impact**: Fixes background replacement "source image can't be found" errors

### ImageViewer Controller Enhancements
- **Container Path Resolution**: Added proper IWebHostEnvironment.WebRootPath usage for Docker containers
- **Enhanced Error Handling**: Added detailed error logging with exception types and stack traces for debugging
- **Debug Tools**: Added comprehensive debugging endpoints:
  - `/ImageViewer/DebugPaths` - Tests all possible file path combinations
  - `/ImageViewer/InspectDatabase` - Shows database record details
- **Background Replacement**: Fixed file path trimming and improved error reporting

### UI/UX Improvements
- **Compact Button Layout**: Reorganized ImageViewer buttons into primary actions + dropdown menu
- **Mobile Responsiveness**: Improved button visibility and text labels across screen sizes
- **Checkbox Styling**: Fixed hard-to-click "Make album public" checkbox with better styling
- **Space Optimization**: Reduced navbar spacing to give more room for image display

### Future Roadmap Considerations
- **Voice Control Integration**: Planning for voice-activated commands for image operations
- **Workflow Streamlining**: Multiple steps currently required for common operations need consolidation
- **Cross-Page Button Integration**: Consider adding background editing buttons to other relevant pages
- **UI Consistency**: Standardize button layouts and styling across the application

### Technical Debt
- **Manual Testing**: Need automated test suite for background replacement functionality
- **Error Handling**: Consider global error handling middleware for better user experience
- **Performance**: Background replacement can be slow - consider progress indicators
- **Caching**: Image cache-busting needs optimization for faster updates

## Known Issues
- Background replacement requires page refresh to see changes
- Container deployments take 5+ minutes, slowing debugging cycles
- Debug tools should be hidden in production builds