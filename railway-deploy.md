# Fish-Smart Railway.app Deployment Guide

## Quick Deploy to Railway.app

### Option 1: One-Click Deploy
1. Visit: https://railway.app
2. Sign up with your GitHub account
3. Click "Deploy from GitHub repo"
4. Select your Fish-Smart repository
5. Railway will automatically detect the Dockerfile and deploy

### Option 2: CLI Deploy (Recommended)
```bash
# Install Railway CLI
npm install -g @railway/cli

# Login to Railway
railway login

# Initialize project in your Fish-Smart directory
cd Fish-Smart
railway init

# Set your environment variables
railway variables set DB_SERVER_FISH_SMART=SQL5112.site4now.net
railway variables set DB_NAME_FISH_SMART=db_a7b035_fishsmart
railway variables set DB_USER_FISH_SMART=db_a7b035_fishsmart_admin
railway variables set DB_PASSWORD_FISH_SMART=BlueSun@001
railway variables set ADMIN_EMAIL_FISH_SMART=Manager@Fish-Smart.com
railway variables set ADMIN_PASSWORD_FISH_SMART=BlueSun@001
railway variables set SYNCFUSION_KEY=Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXtdcHRSQmJZV0NwWEBWYUA=
railway variables set DEFAULT_CITY_FISH_SMART="St Petersburg"
railway variables set DEFAULT_STATE_FISH_SMART=FL
railway variables set DEFAULT_ZIPCODE_FISH_SMART=33715
railway variables set SITE_URL_FISH_SMART=https://Fish-Smart.com
railway variables set SITE_NAME_FISH_SMART=Fish-Smart
railway variables set SITE_DOMAIN_FISH_SMART=Fish-Smart.com
railway variables set SITE_ORG="The Mikish Group"
railway variables set SMTP_SERVER_FISH_SMART=mail5009.site4now.net
railway variables set SMTP_USERNAME_FISH_SMART=Manager@Fish-Smart.com
railway variables set SMTP_PASSWORD_FISH_SMART=BlueSun@001
railway variables set SMTP_PORT=587
railway variables set SMTP_SSL=true

# Deploy your application
railway up
```

## Estimated Monthly Cost

**For your Fish-Smart application:**
- **RAM Usage**: ~1GB = $10/month
- **CPU Usage**: ~0.5 vCPU = $10/month  
- **Storage**: ~1GB = $0.15/month
- **Total Estimated**: ~$20.15/month

**Cost Savings**: $100/month → $20/month = **$80/month savings!**

## Post-Deployment Steps

1. **Domain Setup**: Point Fish-Smart.com to your Railway URL
2. **Database**: Your existing SQL Server will work seamlessly
3. **File Storage**: Consider adding Railway Volumes for persistent files
4. **Monitoring**: Railway provides built-in monitoring and logs

## Rollback Plan

If you need to rollback:
1. Your original hosting remains unchanged
2. Simply update your domain DNS back to original hosting
3. No data loss - your database stays the same

## Next Steps

1. Install Docker Desktop locally (for testing)
2. Test the container build locally
3. Push to GitHub
4. Deploy to Railway
5. Update DNS to point to Railway

Would you like me to help you install Docker Desktop to test locally first?