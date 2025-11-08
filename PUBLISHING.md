# Publishing Guide

Complete guide for publishing your extension to the Visual Studio Code Marketplace.

## Prerequisites

### 1. Create a Visual Studio Marketplace Publisher

1. Go to <https://marketplace.visualstudio.com/>
2. Click "Publish extensions" (top-right)
3. Sign in with your Microsoft account
4. Click your profile icon > "Publisher profile"
5. Create a new publisher with ID: `blue-it-systems`

### 2. Create a Personal Access Token (PAT)

1. Go to <https://dev.azure.com/>
2. Sign in with the same Microsoft account
3. Click on your profile icon > "Personal access tokens"
4. Click "+ New Token"
5. Configure the token:
   - **Name**: VS Code Marketplace Publishing
   - **Organization**: All accessible organizations
   - **Expiration**: Custom defined (e.g., 1 year)
   - **Scopes**: Custom defined
     - Select: **Marketplace > Manage**
6. Click "Create"
7. **IMPORTANT**: Copy the token immediately (you won't see it again!)

### 3. Add PAT to GitHub Secrets

1. Go to your GitHub repository
2. Navigate to: Settings > Secrets and variables > Actions
3. Click "New repository secret"
4. Add secret:
   - **Name**: `VSCE_PAT`
   - **Value**: [paste your Personal Access Token]
5. Click "Add secret"

## Publishing Options

### Option 1: Automated Publishing via GitHub Actions (Recommended)

This is the easiest and most reliable method.

#### Step-by-Step Process

1. **Update version in package.json**:

   ```json
   {
     "version": "0.1.0"
   }
   ```

2. **Update CHANGELOG.md** with new features:

   ```markdown
   ## [0.1.0] - 2025-11-08

   ### Added
   - New feature description
   ```

3. **Commit and push changes**:

   ```bash
   git add package.json CHANGELOG.md
   git commit -m "Release v0.1.0"
   git push origin main
   ```

4. **Create a GitHub Release**:

   - Go to your repository on GitHub
   - Click "Releases" > "Draft a new release"
   - Click "Choose a tag" and create a new tag: `v0.1.0`
   - Title: `v0.1.0`
   - Description: Copy from CHANGELOG.md
   - Click "Publish release"

5. **Automated Publishing**:
   - GitHub Actions will automatically:
     - Build the extension
     - Run linting
     - Create `.vsix` package
     - Publish to VS Code Marketplace
   - Monitor progress: Actions tab in GitHub

6. **Verify Publication**:
   - Go to <https://marketplace.visualstudio.com/publishers/blue-it-systems>
   - Your extension should appear within 5-10 minutes
   - Search for "C# Test Filter Helper" in VS Code

### Option 2: Manual Publishing via Command Line

If you prefer to publish manually:

1. **Install vsce** (if not already installed):

   ```bash
   npm install -g @vscode/vsce
   ```

2. **Login to marketplace** (first time only):

   ```bash
   vsce login blue-it-systems
   ```

   Enter your Personal Access Token when prompted.

3. **Update version**:

   ```bash
   npm version patch  # 0.0.1 -> 0.0.2
   # or
   npm version minor  # 0.0.1 -> 0.1.0
   # or
   npm version major  # 0.0.1 -> 1.0.0
   ```

4. **Package and publish**:

   ```bash
   npm run compile
   vsce publish
   ```

5. **Push changes to GitHub**:

   ```bash
   git push origin main --tags
   ```

### Option 3: Manual Workflow Dispatch

Trigger publishing without creating a release:

1. Go to GitHub repository > Actions
2. Select "Publish VS Code Extension" workflow
3. Click "Run workflow"
4. Select branch: `main`
5. Click "Run workflow"

## Version Numbering

Follow [Semantic Versioning](https://semver.org/):

- **MAJOR** (1.0.0): Breaking changes
- **MINOR** (0.1.0): New features (backward compatible)
- **PATCH** (0.0.1): Bug fixes

Example progression:

```text
0.0.1 -> 0.0.2 (bug fix)
0.0.2 -> 0.1.0 (new feature)
0.1.0 -> 1.0.0 (stable release)
```

## Pre-Release Publishing

For beta testing:

1. **Update version with pre-release tag**:

   ```json
   {
     "version": "0.1.0-beta.1"
   }
   ```

2. **Publish as pre-release**:

   ```bash
   vsce publish --pre-release
   ```

## Updating an Existing Extension

1. Make your changes
2. Update version number (bump patch/minor/major)
3. Update CHANGELOG.md
4. Follow publishing steps above
5. Users will receive automatic updates

## Unpublishing an Extension

If you need to remove the extension:

```bash
vsce unpublish blue-it-systems.csharp-test-filter
```

**Warning**: This removes the extension for all users!

## Troubleshooting

### Error: "Failed to publish - already exists"

- You're trying to publish the same version twice
- Update the version in `package.json`

### Error: "Invalid Personal Access Token"

- Token expired or incorrect
- Create a new PAT and update GitHub secret

### Error: "Publisher not found"

- Publisher ID in `package.json` doesn't match
- Verify publisher ID matches your Marketplace account

### Extension not appearing in Marketplace

- Wait 5-10 minutes for indexing
- Check publisher dashboard for status
- Verify no errors in GitHub Actions logs

### GitHub Action fails

- Check if `VSCE_PAT` secret is set correctly
- Verify PAT has not expired
- Check Action logs for specific error

## Monitoring Your Extension

### View Statistics

1. Go to <https://marketplace.visualstudio.com/manage>
2. Sign in
3. View your extension metrics:
   - Install count
   - Download count
   - Ratings and reviews

### View GitHub Action Runs

1. Go to your repository
2. Click "Actions" tab
3. View build and publish history

## Best Practices

1. **Always test locally** before publishing
2. **Update CHANGELOG.md** for every release
3. **Use semantic versioning** consistently
4. **Create GitHub releases** for major versions
5. **Monitor extension reviews** and respond to issues
6. **Keep dependencies updated** regularly
7. **Test on multiple platforms** (Windows, Mac, Linux)

## Support and Issues

If you encounter problems:

1. Check GitHub Actions logs for errors
2. Review VS Code extension publishing docs
3. Check Azure DevOps PAT settings
4. File an issue in the repository

## Additional Resources

- [VS Code Extension API](https://code.visualstudio.com/api)
- [Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension)
- [Extension Manifest](https://code.visualstudio.com/api/references/extension-manifest)
- [vsce CLI Reference](https://github.com/microsoft/vscode-vsce)
