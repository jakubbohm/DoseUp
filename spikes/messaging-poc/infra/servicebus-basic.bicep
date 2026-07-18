// ============================================================================
// Spike #50 — a real Azure Service Bus BASIC namespace to smoke-test against.
// The emulator emulates the Standard surface, so it cannot catch a Basic-tier
// violation; this template is deliberately QUEUES-ONLY (no topics/subscriptions),
// so a Basic-incompatible design would fail at deploy — that is part of the proof.
//
// Hand-authored per ADR-0004 (Azure is defined only by Bicep). Throwaway spike
// scaffold: the production namespace lands in infra/ with the M0 messaging change.
// ============================================================================

@description('Azure region. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Globally-unique Service Bus namespace name (3-50 chars, alphanumeric + hyphens).')
param namespaceName string

// Basic tier: queues only — no topics, no subscriptions, no sessions. ~$0.05/M ops.
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

// The one queue the PoC uses. Basic-tier-safe settings only:
// no requiresSession, no requiresDuplicateDetection (both Standard+).
resource doseEvents 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'dose-events'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'PT1H'
  }
}

@description('The namespace FQDN — for the managed-identity path (UseAzureServiceBus(fqdn, cred)).')
output namespaceHostName string = '${serviceBus.name}.servicebus.windows.net'

@description('''Fetch the connection string for the quick smoke (spike only; prod uses managed identity):
  az servicebus namespace authorization-rule keys list \
    --resource-group <rg> --namespace-name <namespaceName> \
    --name RootManageSharedAccessKey --query primaryConnectionString -o tsv''')
output namespaceName string = serviceBus.name
