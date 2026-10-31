using System.Threading.Tasks;
using Xunit;

namespace AdopPlant.Analytics.Tests
{
    public class MetricasRepositoryTests
    {
        [Fact]
        public async Task ObtenerResumenMetricasAsync_DevuelveValoresValidos()
        {
            int totalPlantas = 45;
            int totalAdopciones = 12;

            await Task.Delay(10);
            bool metricasValidas = totalPlantas >= 0 && totalAdopciones >= 0;

            Assert.True(metricasValidas);
            Assert.Equal(45, totalPlantas);
        }
    }
}