export const environment = {
  production: false,
  apiBase: 'http://localhost:5100',
  signalRHub: 'http://localhost:5004/hubs/notifications',
  grafanaBase: 'http://localhost:3000',
  grafanaDashboards: {
    serviceHealth: '/d/service-health/service-health',
    equipmentMetrics: '/d/equipment-metrics/equipment-metrics',
  },
};
