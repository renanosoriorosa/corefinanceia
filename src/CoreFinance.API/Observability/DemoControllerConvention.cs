using CoreFinance.API.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace CoreFinance.API.Observability;

// Interruptor do DemoController: com Observability:Demo:Enabled desligado, o controller e
// removido do modelo da aplicacao antes do roteamento existir. Nao e um filtro devolvendo 404 —
// as rotas simplesmente nao chegam a nascer, e o Swagger tambem nao as enxerga.
//
// Funcionalidade de laboratorio precisa de interruptor, e o interruptor mora na configuracao,
// nao em #if DEBUG: assim da para ligar e desligar sem recompilar.
public sealed class DemoControllerConvention : IApplicationModelConvention
{
    private readonly bool _habilitado;

    public DemoControllerConvention(bool habilitado)
    {
        _habilitado = habilitado;
    }

    public void Apply(ApplicationModel application)
    {
        if (_habilitado)
            return;

        var demo = application.Controllers
            .FirstOrDefault(controller => controller.ControllerType == typeof(DemoController));

        if (demo is not null)
            application.Controllers.Remove(demo);
    }
}
