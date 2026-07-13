export const environment = {
  production: true,
  apiBase: '',           // same-origin via YARP
  signalRHub: '/hubs/notifications',
  grafanaBase: 'http://localhost:3000',
  grafanaDashboards: {
    serviceHealth: '/d/service-health/service-health',
    equipmentMetrics: '/d/equipment-metrics/equipment-metrics',
  },
};
