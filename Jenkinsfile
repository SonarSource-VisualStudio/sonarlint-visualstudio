#!/bin/groovy
@Library('SonarSource@2.1.1') _

pipeline {
  agent none
  environment {         
    MAVEN_TOOL = 'Maven 3.3.x'
    PFX_PASSWORD = credentials('pfx-passphrase')
    GITHUB_TOKEN = credentials('sonartech-github-token')
    SONARCLOUD_TOKEN = credentials('SONARCLOUD_TOKEN')
    ARTIFACTS_TO_DOWNLOAD = 'org.sonarsource.dotnet:SonarLint.VSIX:vsix:2015,org.sonarsource.dotnet:SonarLint.VSIX:vsix:2017,org.sonarsource.dotnet:SonarLint.VSIX:vsix:2019'
  }
  stages{
    stage('NotifyStart')  {
      steps{
        sendAllNotificationBuildStarted()
      }
    }
    stage('Build') {
      parallel {
        stage('vs2015'){
          agent { 
            label 'vs2015' 
          }
          tools {
            jdk 'Java 8'
          }
          steps{
            bat script: 'ci-build.cmd'
            stash includes: 'binaries/SonarLint.2015.vsix', name: "vsix-vs2015"
          }
        }
        stage('vs2017') {
          agent { 
            label 'vs2017' 
          }
          tools {
            jdk 'Java 8'
          }
          steps{
            bat script: 'ci-build.cmd'
            stash includes: 'binaries/SonarLint.2017.vsix', name: "vsix-vs2017"
            stash includes: 'binaries/SonarLint.2019.vsix', name: "vsix-vs2019"
          }
        }
      }
      post {
        always {
          sendAllNotificationBuildResult()
        }
      }       
    }     
      
    stage('Deploy') {   
      //'master'.equals(env.GITHUB_BRANCH) || 'refs/heads/master'.equals(env.GITHUB_BRANCH) || 'true'.equals(env.IS_PULLREQUEST)
      when{
        anyOf {
          environment name: 'GITHUB_BRANCH', value: 'master'
          environment name: 'IS_PULLREQUEST', value: 'true'
        }
      }
      agent {
        label 'linux'
      }
      environment {         
        ARTIFACTORY_DEPLOY_REPO="sonarsource-public-qa"
        REPOX_DEPLOYER=credentials('repox-deploy')
        ARTIFACTORY_DEPLOY_USERNAME="$REPOX_DEPLOYER_USR"
        ARTIFACTORY_DEPLOY_PASSWORD="$REPOX_DEPLOYER_PSW"
        BUILD_ID="${env.BUILD_NUMBER}"
      }
      steps{
        unstash 'vsix-vs2015'
        unstash 'vsix-vs2017'
        unstash 'vsix-vs2019'
        script {
          version = sh returnStdout: true, script: 'cat build/Version.props | grep MainVersion\\> | cut -d \\> -f 2 | cut --complement -d \\< -f 2'
          version = version.trim() + ".${env.BUILD_NUMBER}"
        } 
        echo "${version}"
        dir('build/poms') {
          withMaven(maven: MAVEN_TOOL) {
            sh "mvn -B versions:set -DgenerateBackupPoms=false -DnewVersion=${version}"
            withEnv(["PROJECT_VERSION=${version}"]) {
              sh "mvn deploy -Pdeploy-sonarsource -B -e -V"
            }
          }
        }             
      }       
      post {
        always {
          sendAllNotificationBuildResult()
        }
      }
    }

    stage('QA')  {
      when{
        anyOf {
          environment name: 'GITHUB_BRANCH', value: 'master'
          environment name: 'IS_PULLREQUEST', value: 'true'
        }
      }
      steps{
        //at the moment no QA is executed for sonarlint-visualstudio
        sendAllNotificationQaStarted()
      }
      post {
        always {
          sendAllNotificationQaResult()
        }
      }
    } 

    stage('Promote') {
      when{
        anyOf {
          environment name: 'GITHUB_BRANCH', value: 'master'
          environment name: 'IS_PULLREQUEST', value: 'true'
        }
      } 
      steps {
        repoxPromoteBuild()
      }
      post {
        always {
          sendAllNotificationPromote()
        }
      }
    }     
  }  
}



