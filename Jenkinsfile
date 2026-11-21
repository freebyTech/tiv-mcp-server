@Library('freebyTech')_

import com.freebyTech.BuildInfo

String versionPrefix = '0.1'
String repository = 'trackitsvalue'
String imageName = 'tiv-mcp-budget'
String dockerBuildArguments = ''
BuildInfo buildInfo

node
{
  // Builds ./src/Dockerfile (restore, build, unit tests, publish), pushes the image and packages
  // the Helm chart in ./deploy/tiv-mcp-budget. The registry URL and all credentials come from the
  // Jenkins environment, never from this repository.
  buildInfo = build(this, versionPrefix, repository, imageName, dockerBuildArguments, true, true)

  if('main'.equalsIgnoreCase(env.BRANCH_NAME))
  {
    env.NAMESPACE = 'production'
    deploy(buildInfo, repository, imageName, './env/production.yaml')
  }
  else if('develop'.equalsIgnoreCase(env.BRANCH_NAME))
  {
    env.NAMESPACE = 'beta'
    deploy(buildInfo, repository, imageName, './env/beta.yaml')
  }
}
