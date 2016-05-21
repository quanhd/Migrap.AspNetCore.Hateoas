﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Migrap.AspNetCore.Hateoas.Siren.Converters;
using Migrap.AspNetCore.Hateoas.Siren.Internal;
using Newtonsoft.Json;

namespace Migrap.AspNetCore.Hateoas.Siren {
    public class SirenOutputFormatter : JsonOutputFormatter {
        public SirenOutputFormatter()
            : this(SirenSerializerSettingsProvider.CreateSerializerSettings()) {
        }

        public SirenOutputFormatter(JsonSerializerSettings serializerSettings) {
            if(serializerSettings == null) {
                throw new ArgumentNullException(nameof(serializerSettings));
            }

            SupportedMediaTypes.Clear();

            SupportedEncodings.Add(Encoding.UTF8);
            SupportedMediaTypes.Add(MediaTypeHeaderValues.ApplicationSiren);

            SerializerSettings = serializerSettings;
            SerializerSettings.Converters.Add(new HrefJsonConverter());
            SerializerSettings.ContractResolver = new SirenCamelCasePropertyNamesContractResolver();
        }

        public IList<IStateConverterProvider> Converters { get; } = new List<IStateConverterProvider>();

        public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding) {
            var response = context.HttpContext.Response;
            var converters = GetDefaultConverters(context);

            var converterProviderContext = new StateConverterProviderContext {
                ObjectType = context.ObjectType
            };

            var selectedConverter = SelectConverter(converterProviderContext, converters);

            if(selectedConverter == null) {
                context.FailedContentNegotiation = true;
                return;
            }

            var converterContext = new StateConverterContext {
                HttpContext = context.HttpContext,
                Object = context.Object,
                ObjectType = context.ObjectType,
            };

            var document = await selectedConverter.ConvertAsync(converterContext);

            using(var writer = context.WriterFactory(response.Body, selectedEncoding)) {
                WriteObject(writer, document);

                await writer.FlushAsync();
            }
        }

        private IEnumerable<IStateConverterProvider> GetDefaultConverters(OutputFormatterWriteContext context) {
            var converters = default(IEnumerable<IStateConverterProvider>);

            if(Converters == null || Converters.Count == 0) {
                var options = context
                    .HttpContext
                    .RequestServices
                    .GetRequiredService<IOptions<SirenOptions>>()
                    .Value;
                converters = options.Converters;
            }

            return converters;
        }

        public virtual IStateConverter SelectConverter(StateConverterProviderContext context, IEnumerable<IStateConverterProvider> converters) {
            return converters.Select(x => x.CreateConverter(context)).FirstOrDefault(x => x != null);
        }
    }
}