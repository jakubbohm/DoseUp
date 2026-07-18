// Spike #50 — parameters for the Basic-tier smoke namespace. Pick a globally-unique name.
using './servicebus-basic.bicep'

param namespaceName = 'doseup-spike-sb-basic'
// location defaults to the resource group's location.
