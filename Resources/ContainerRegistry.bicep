@sys.description('The name of the instance.')
@minLength(1)
param name string

@sys.description('The location of the instance.')
param location string = resourceGroup().location

@sys.description('The sku of the instance.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Basic'

resource symbolicname 'Microsoft.ContainerRegistry/registries@2021-09-01' = {
  name: name
  location: location
  sku: {
    name: sku
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}
