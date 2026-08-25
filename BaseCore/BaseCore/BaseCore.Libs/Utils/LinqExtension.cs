using System;
using System.Linq;
using System.Linq.Expressions;

namespace BaseCore.Libs.Utils
{
    /// <summary>
    /// Class use to order by list objects
    /// </summary>
    public static class LinqExtension
    {
        public static IQueryable<T> OrderByField<T>(this IQueryable<T> q, string fieldName, bool asc)
        {
            var param = Expression.Parameter(typeof(T), "p");
            var prop = Expression.Property(param, fieldName);
            var exp = Expression.Lambda(prop, param);
            var method = asc ? "OrderBy" : "OrderByDescending";
            var types = new Type[] { q.ElementType, exp.Body.Type };
            var mce = Expression.Call(typeof(Queryable), method, types, q.Expression, exp);
            return q.Provider.CreateQuery<T>(mce);
        }

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string ordering, params object[] values)
        {
            ordering = !string.IsNullOrEmpty(ordering) ? ordering : "Id";
            var type = typeof(T);
            var property = type.GetProperty(ordering.FirstCharToUpper());
            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);
            MethodCallExpression resultExp = Expression.Call(typeof(Queryable), "OrderBy", new Type[] { type, property.PropertyType }, source.Expression, Expression.Quote(orderByExp));
            return source.Provider.CreateQuery<T>(resultExp);
        }

        public static IQueryable<T> OrderByDescending<T>(this IQueryable<T> source, string ordering, params object[] values)
        {
            var type = typeof(T);
            var property = type.GetProperty(ordering.FirstCharToUpper());
            var parameter = Expression.Parameter(type, "p");
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var orderByExp = Expression.Lambda(propertyAccess, parameter);
            MethodCallExpression resultExp = Expression.Call(typeof(Queryable), "OrderByDescending", new Type[] { type, property.PropertyType }, source.Expression, Expression.Quote(orderByExp));
            return source.Provider.CreateQuery<T>(resultExp);
        }

        public static string FirstCharToUpper(this string input)
        {
            return input.First().ToString().ToUpper() + input.Substring(1);
        }
    }
}
