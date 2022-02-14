@sys.description('The location of the resources')
param location string = resourceGroup().location

module ContainerRegistryModule './ContainerRegistry.bicep' = {
  name: 'ContainerRegistryDeploy'
  params: {
    name: 'shipshapecontainerregistry'
    location: location
  }
}
