﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace ImisRestApi.Data
{
    public class DataHelper
    {
        private readonly string ConnectionString;

        public int ReturnValue { get; set; }

        public DataHelper(IConfiguration configuration)
        {
            ConnectionString = configuration["ConnectionStrings:DefaultConnection"];
        }

        public DataSet FillDataSet(string SQL, SqlParameter[] parameters, CommandType commandType)
        {
            DataSet ds = new DataSet();
            var sqlConnection = new SqlConnection(ConnectionString);

            SqlParameter returnParameter = new SqlParameter("@RV", SqlDbType.Int);
            returnParameter.Direction = ParameterDirection.ReturnValue;
            
            var command = new SqlCommand(SQL, sqlConnection)
            {
                CommandType = commandType
            };
            command.Parameters.Add(returnParameter);

            var adapter = new SqlDataAdapter(command);
            using (command)
            {
                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);
                adapter.Fill(ds);
               
            }

            ReturnValue = int.Parse(returnParameter.Value.ToString());

            return ds;
        }

        public List<string> GetUserRights(string userId)
        {
            var rights = new List<string>();

            var sSQL = @"SELECT tblRoleRight.RightID
                         FROM   tblRole INNER JOIN
                         tblRoleRight ON tblRole.RoleID = tblRoleRight.RoleID INNER JOIN
                         tblUserRole ON tblRole.RoleID = tblUserRole.RoleID INNER JOIN
                         tblUsers ON tblUserRole.UserID = tblUsers.UserID
                         WHERE tblUsers.UserID = @UserId AND tblUsers.ValidityTo IS NULL";

            SqlParameter[] paramets = {
                new SqlParameter("@UserId", userId)
            };

            var dt = GetDataTable(sSQL, paramets, CommandType.Text);

            if (dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++) {
                    var row = dt.Rows[i];
                    var rightId = Convert.ToInt32(row["RightID"]);
                    var rightName = Enum.GetName(typeof(Models.Rights), rightId);

                    if (rightName != null) {
                        rights.Add(rightName);
                    }
                    
                }
            }

            return rights;
        }

        public DataTable GetDataTable(string SQL, SqlParameter[] parameters, CommandType commandType)
        {
            DataTable dt = new DataTable();
            var sqlConnection = new SqlConnection(ConnectionString);
            var command = new SqlCommand(SQL, sqlConnection)
            {
                CommandType = commandType
            };

            var adapter = new SqlDataAdapter(command);

            using (command)
            {
                if (parameters.Length > 0)
                    command.Parameters.AddRange(parameters);
                adapter.Fill(dt);
            }

            return dt;
        }

        public void Execute(string SQL, SqlParameter[] parameters, CommandType commandType)
        {
            var sqlConnection = new SqlConnection(ConnectionString);

            //if(SqlCommand.C)
            // sqlConnection.Open
            var command = new SqlCommand(SQL, sqlConnection)
            {
                CommandType = commandType
            };

            using (command)
            {
                if (command.Connection.State == 0)
                {
                    command.Connection.Open();

                    if (parameters.Length > 0)
                        command.Parameters.AddRange(parameters);

                    command.ExecuteNonQuery();

                    command.Connection.Close();
                }

            }
        }

        public int Procedure(string StoredProcedure, SqlParameter[] parameters)
        {
            SqlConnection sqlConnection = new SqlConnection(ConnectionString);
            SqlCommand command = new SqlCommand();
            SqlDataReader reader;

            SqlParameter returnParameter = new SqlParameter("@RV", SqlDbType.Int);
            returnParameter.Direction = ParameterDirection.ReturnValue;

            command.CommandText = StoredProcedure;
            command.CommandType = CommandType.StoredProcedure;
            command.Connection = sqlConnection;
            command.Parameters.Add(returnParameter);

            if (parameters.Length > 0)
                command.Parameters.AddRange(parameters);

            sqlConnection.Open();

            command.ExecuteNonQuery();

            int rv = int.Parse(returnParameter.Value.ToString());
            // var message = new ResponseMessage(rv).Message;

            sqlConnection.Close();

            return rv;
        }

        public IList<SqlParameter> ExecProcedure(string StoredProcedure, SqlParameter[] parameters)
        {
            SqlConnection sqlConnection = new SqlConnection(ConnectionString);
            SqlCommand command = new SqlCommand();

            SqlParameter returnParameter = new SqlParameter("@RV", SqlDbType.Int);
            returnParameter.Direction = ParameterDirection.ReturnValue;
            
            command.CommandText = StoredProcedure;
            command.CommandType = CommandType.StoredProcedure;
            command.Connection = sqlConnection;
            command.Parameters.Add(returnParameter);

            if (parameters.Length > 0)
                command.Parameters.AddRange(parameters);

            sqlConnection.Open();

            command.ExecuteNonQuery();

            var rv = parameters.Where(x => x.Direction.Equals(ParameterDirection.Output) || x.Direction.Equals(ParameterDirection.ReturnValue)).ToList();
            rv.Add(returnParameter);

            sqlConnection.Close();

            return rv;
        }

        public DataTable FindUserByName(string UserName)
        {
            var sSQL = @"
                        SELECT UserID,LoginName, LanguageID, RoleID,StoredPassword,PrivateKey
                        FROM tblUsers
                        WHERE LoginName = @LoginName                        
                        AND ValidityTo is null
                        ";

            SqlParameter[] paramets = {
                new SqlParameter("@LoginName", UserName)
            };

            //var data = new DataHelper(Configuration);

            var dt = GetDataTable(sSQL, paramets, CommandType.Text);

            return dt;
        }

    }
}
